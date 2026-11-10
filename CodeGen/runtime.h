#ifndef STDC_H
#define STDC_H
#include <stddef.h>
#define STACK_SIZE 999
typedef struct StackFrame
{
    int line;
    const char *file;

} StackFrame;
StackFrame stack[STACK_SIZE];
int top = -1;

void writeLine(int space, const char *file, int targetLine);
void printError(const char *errorName, const char *message, const char *file, int line);
void pushFrame(int line, const char *file);
void *gc_alloc(size_t size, void *vtable);
void gc_retainStrong(void *ptr);
void gc_retainWeak(void *ptr);
void gc_releaseWeak(void *ptr);
void gc_releaseStrong(void *ptr);
void *gc_upgradeWeak(void *ptr);
void printStackTrace();
#endif // STDC_H