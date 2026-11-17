<div align="center">

# BifyLang

**A statically typed, compiled programming language powered by LLVM 23 and Automatic Reference Counting.**

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![LLVM](https://img.shields.io/badge/LLVM-23-262D3A?style=flat-square&logo=llvm)
![Platform](https://img.shields.io/badge/Platform-Linux%20x64-FCC624?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

</div>

```text
Source (.bify) ──▶ Lexer ──▶ Parser / AST ──▶ LLVM 23 IR CodeGen ──▶ clang + runtime.o ──▶ Native Binary
```

## Features

- **Static typing** – Variables, expressions, and function calls are type-checked during compilation.
- **LLVM backend** – Generates LLVM IR and compiles programs into native binaries.
- **Object-oriented programming** – Supports classes, access modifiers, constructors, destructors, single inheritance, and virtual method dispatch through `virtual` and `override`.
- **Automatic Reference Counting (ARC)** – Manages object lifetimes using strong and weak references. Reference counting is handled by a small C runtime.
- **Control flow** – Supports `if`/`else`, `for`, `while`, and `switch` statements.
- **Modules** – Allows code to be split across multiple files and imported where needed.

## Requirements

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- `clang` – Required in `PATH` to compile and link the C runtime.

### NuGet Dependencies (restored automatically)
- `libLLVM` & `libLLVM.runtime.linux-x64` (23.1.0) – Native LLVM 23 bindings for Linux x64.
- `LLVMSharp` & `LLVMSharp.Interop` (20.1.2) – .NET bindings for LLVM IR generation.
- `Spectre.Console` & `System.CommandLine` – Rich console diagnostics and CLI parsing.
- `NUnit` (4.2.2) – Test framework used by `BifyLang.Tests`.

## Getting Started

Build the compiler and C runtime:

```bash
dotnet build
```

Compile and run a source file:

```bash
dotnet run -- hello.bify --exec
```

### CLI Usage

```bash
dotnet run -- <input> [options]
```

| Argument | Description |
|---|---|
| `<input>` | Path to the source `.bify` file. |
| `-o`, `--output <file>` | Specify where the compiled output should be written. |
| `-e`, `--exec` | Execute the compiled program after compilation. |

## Examples

### Hello World

A simple program that prints a message:

`hello.bify`:

```c
int main() {
    printf("Hello %s\n", "Bify");
    return 0;
}
```

### Classes and Inheritance

BifyLang supports inheritance and virtual method overriding. The following example also demonstrates weak references:

```c
class Animal {
    public virtual int speak() {
        return 1;
    }
}

class Dog : Animal {
    public override int speak() {
        return 2;
    }
}

int main() {
    Animal a = new Dog();
    weak Animal w = a;

    printf("%d\n", w.speak());
    return 0;
}
```

<details>
<summary><b>Inspect Generated LLVM IR</b></summary>

```llvm
; ModuleID = 'main'
source_filename = "main"

%Dog = type { ptr, i32, i32 }
%Animal = type { ptr, i32, i32 }

@test.Animal.vtable = internal constant [2 x ptr] [ptr @test.Animal_destructor, ptr @test.Animal_speak]
@test.Dog.vtable = internal constant [2 x ptr] [ptr @test.Dog_destructor, ptr @test.Dog_speak]
@.str = private constant [4 x i8] c"%d\0A\00"

declare ptr @gc_alloc(i64, ptr)

declare void @gc_retainStrong(ptr)

declare void @gc_retainWeak(ptr)

declare void @gc_releaseStrong(ptr)

declare void @gc_releaseWeak(ptr)

declare ptr @gc_upgradeWeak(ptr)

declare i32 @printf(ptr, ...)

define void @test.Animal_destructor(ptr %0) {
entry:
  %this = alloca ptr, align 8
  store ptr %0, ptr %this, align 8
  br label %dtorCleanup

dtorCleanup:                                      ; preds = %entry
  ret void
}

define i32 @test.Animal_speak(ptr %0) {
entry:
  %this = alloca ptr, align 8
  store ptr %0, ptr %this, align 8
  ret i32 1
}

define void @test.Dog_destructor(ptr %0) {
entry:
  %this = alloca ptr, align 8
  store ptr %0, ptr %this, align 8
  br label %dtorCleanup

dtorCleanup:                                      ; preds = %entry
  call void @test.Animal_destructor(ptr %0)
  ret void
}

define i32 @test.Dog_speak(ptr %0) {
entry:
  %this = alloca ptr, align 8
  store ptr %0, ptr %this, align 8
  ret i32 2
}

define i32 @test.main() {
entry:
  %inst_ptr = call ptr @gc_alloc(i64 ptrtoint (ptr getelementptr (%Dog, ptr null, i32 1) to i64), ptr @test.Dog.vtable)
  %a = alloca ptr, align 8
  store ptr %inst_ptr, ptr %a, align 8
  %a1 = load ptr, ptr %a, align 8
  %w = alloca ptr, align 8
  call void @gc_retainWeak(ptr %a1)
  store ptr %a1, ptr %w, align 8
  %w2 = load ptr, ptr %w, align 8
  %upgraded_ptr = call ptr @gc_upgradeWeak(ptr %w2)
  %vptr_slot = getelementptr inbounds nuw %Animal, ptr %upgraded_ptr, i32 0, i32 0
  %vtable = load ptr, ptr %vptr_slot, align 8
  %slot_ptr = getelementptr ptr, ptr %vtable, i32 1
  %speak_fn = load ptr, ptr %slot_ptr, align 8
  %0 = call i32 %speak_fn(ptr %upgraded_ptr)
  %1 = call i32 (ptr, ...) @printf(ptr @.str, i32 %0)
  call void @gc_releaseStrong(ptr %upgraded_ptr)
  %cleanup_w = load ptr, ptr %w, align 8
  call void @gc_releaseWeak(ptr %cleanup_w)
  %cleanup_a = load ptr, ptr %a, align 8
  call void @gc_releaseStrong(ptr %cleanup_a)
  ret i32 0
}

define i32 @main() {
entry:
  %exit_code = call i32 @test.main()
  ret i32 %exit_code
}
```

</details>

## Running Tests

Run the test suite using:

```bash
dotnet test
```

## Roadmap

- [x] Core lexer, parser, and AST with error recovery diagnostics
- [x] LLVM 23 IR codegen and native executable compilation
- [x] Classes, inheritance, VTables (`virtual`/`override`), constructors, and destructors
- [x] ARC memory management with strong and `weak` references
- [x] Multi-file modules and basic C interop (`printf`)
- [ ] Arrays, rich string support, and standard library primitives
- [ ] Union types

## License

This project is licensed under the [MIT License](LICENSE).