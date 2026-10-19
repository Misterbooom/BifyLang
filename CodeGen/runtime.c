#include <stdint.h>
#include <stdlib.h>
#include <stdint.h>
#define OBJECT_VPTR_OFFSET 0
#define OBJECT_STRONG_OFFSET 8
#define OBJECT_WEAK_OFFSET 12
#include <stdio.h>
#include <string.h>
#include "runtime.h"
typedef void (*DestructorFunc)(void *);


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

        snprintf(fileInfo, sizeof(fileInfo), "     File '%s', Line: %d", frame.file, frame.line);

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

void gc_retainStrong(void *ptr)
{
    if (ptr == NULL) return;
    (*object_strong_ptr(ptr))++;
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
    (*strongCount)--;
    if (*strongCount == 0)
    {
        gc_releaseWeak(ptr);
    }
}

void *gc_upgradeWeak(void *ptr)
{
    if (ptr == NULL || *object_strong_ptr(ptr) == 0)
    {
        return NULL;
    }
    (*object_strong_ptr(ptr))++;
    return ptr; 
}