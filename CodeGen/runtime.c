#include <stdint.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>

#define OBJECT_VPTR_OFFSET 0
#define OBJECT_STRONG_OFFSET 8
#define OBJECT_WEAK_OFFSET 12

#define STACK_SIZE 256

typedef void (*DestructorFunc)(void *);

typedef struct {
    int line;
    const char *file;
} StackFrame;

static StackFrame stack[STACK_SIZE];
static int top = -1;

void writeLine(int space, const char *file, int targetLine)
{
    char res[256] = "";
    for (int i = 0; i < space && strlen(res) < sizeof(res) - 1; i++)
    {
        strncat(res, " ", sizeof(res) - strlen(res) - 1);
    }

    FILE *fptr = fopen(file, "r");
    if (fptr == NULL)
    {
        return;
    }

    int currentLine = 0;
    char line[256];
    while (fgets(line, sizeof(line), fptr) != NULL)
    {
        currentLine++;
        if (currentLine == targetLine)
        {
            strncat(res, line, sizeof(res) - strlen(res) - 1);
            break;
        }
    }

    fclose(fptr);
    printf("%s", res);
}

void printStackTrace()
{
    int start = (top >= 4) ? top - 4 : 0;
    for (int i = top; i >= start; i--)
    {
        StackFrame frame = stack[i];
        char fileInfo[256];
        snprintf(fileInfo, sizeof(fileInfo), "      File '%s', Line: %d", frame.file, frame.line);
        printf("\x1B[31m%s\x1B[0m\n", fileInfo);
        writeLine(10, frame.file, frame.line);
    }
}

void pushFrame(int line, const char *file)
{
    if (top >= STACK_SIZE - 1)
    {
        printf("\x1B[31mStackOverflow: Recursion depth exceeded!\x1B[0m\n");
        printStackTrace();
        fflush(stdout);
        exit(1);
    }

    StackFrame stackFrame;
    stackFrame.line = line;
    stackFrame.file = file;
    top += 1;
    stack[top] = stackFrame;
}

void popFrame()
{
    if (top > -1)
    {
        top -= 1;
    }
}

void printError(const char *errorName, const char *message, const char *file, int line)
{
    pushFrame(line, file);
    char exceptionInfo[256];
    snprintf(exceptionInfo, sizeof(exceptionInfo), "%s: %s", errorName, message);

    printf("\x1B[31m%s\x1B[0m\n", exceptionInfo);
    printStackTrace();

    fflush(stdout);
    exit(1);
}

uint32_t *object_strong_ptr(void *object)
{
    return (uint32_t *)((uint8_t *)object + OBJECT_STRONG_OFFSET);
}

uint32_t *object_weak_ptr(void *object)
{
    return (uint32_t *)((uint8_t *)object + OBJECT_WEAK_OFFSET);
}

void *gc_alloc(size_t size, void *vtable)
{
    void *ptr = calloc(1, size);
    if (ptr == NULL)
    {
        printError("MemoryError", "Out of memory during gc_alloc", __FILE__, __LINE__);
    }

    *(void **)((uint8_t *)ptr + OBJECT_VPTR_OFFSET) = vtable;
    *object_strong_ptr(ptr) = 1;
    *object_weak_ptr(ptr) = 1;

    return ptr;
}

void gc_retainStrong(void *ptr)
{
    if (ptr == NULL) return;

    uint32_t *strongCount = object_strong_ptr(ptr);
    if (*strongCount == 0)
    {
        printError("MemoryError", "ARC Use-After-Free: gc_retainStrong called on dead object!", __FILE__, __LINE__);
    }
    (*strongCount)++;
}

void gc_retainWeak(void *ptr)
{
    if (ptr == NULL) return;
    (*object_weak_ptr(ptr))++;
}

void gc_releaseWeak(void *ptr)
{
    if (ptr == NULL) return;

    uint32_t *weakCount = object_weak_ptr(ptr);
    if (*weakCount == 0)
    {
        printError("MemoryError", "ARC Double Free: gc_releaseWeak underflow!", __FILE__, __LINE__);
    }

    (*weakCount)--;
    if (*weakCount == 0)
    {
        free(ptr);
    }
}

void gc_releaseStrong(void *ptr)
{
    if (ptr == NULL) return;

    uint32_t *strongCount = object_strong_ptr(ptr);
    if (*strongCount == 0)
    {
        printError("MemoryError", "ARC Double Free: gc_releaseStrong called on dead object!", __FILE__, __LINE__);
    }

    (*strongCount)--;
    if (*strongCount == 0)
    {
        void **vtable = *(void ***)ptr;
        if (vtable != NULL && vtable[0] != NULL)
        {
            DestructorFunc dtor = (DestructorFunc)vtable[0];
            dtor(ptr);
        }

        gc_releaseWeak(ptr);
    }
}

void *gc_upgradeWeak(void *ptr)
{
    if (ptr == NULL) return NULL;

    uint32_t *strongCount = object_strong_ptr(ptr);
    if (*strongCount == 0)
    {
        return NULL;
    }

    (*strongCount)++;
    return ptr;
}