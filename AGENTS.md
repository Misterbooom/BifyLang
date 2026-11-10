# BifyLang (BoomifyCS) — AI Agent Architecture & Codegen Rules

## 1. STOP-AND-ASK PROTOCOL (Mandatory)
Before writing or modifying ANY code for a new feature, memory layout change, or multi-file refactoring:
1. **HALT** and output a short Architectural Proposal (max 10-15 lines).
2. Explicitly specify:
    - Which existing classes/files will be touched.
    - What new helper/emitter classes will be created.
    - How LLVM IR types, GEP indices, or VTable/ARC layouts are affected.
3. **WAIT** for user approval before generating implementation code.
4. Only skip this step for trivial, isolated bug fixes (< 30 lines in a single method).

## 2. ANTI-MONOLITH & MODULARITY CONSTRAINTS
- **Hard File Limit:** No `.cs` file may exceed **200 lines**. If an existing file approaches or exceeds this limit (e.g., `ClassFieldParser.cs`, `ControlFlowGen.cs`), split the logic into dedicated sub-handlers before adding new code.
- **Hard Method Limit:** No method may exceed **40 lines**. Extract complex LLVM IR sequences into focused private methods or helper structs.
- **Single Responsibility in CodeGen:**
    - `DeclarationDispatcher`, `StatementDispatcher`, and `ExpressionDispatcher` are strictly for **routing** AST nodes. Never write raw `LLVMBuilder` instructions inside dispatchers.
    - Complex mechanics MUST live in isolated helpers (e.g., `BranchScope`, `BranchEmitter`, `TypeCaster`, `HeapControl`).
    - Use `IDisposable` scopes (`using (...)`) for lexical scopes (`_ctx.EnterScope()`), loop contexts (`_ctx.EnterLoop()`), and automatic basic-block termination (`BranchScope`).

## 3. LLVM IR & MEMORY LAYOUT INVARIANTS (CRITICAL)
Never guess or improvise struct layouts or GEP indices. Follow the strict BifyLang ABI:
- **Object Layout (Class Struct):**
    - Index `0`: `vptr` (Pointer to VTable, `ptr` / `i8*`)
    - Index `1`: `strong_count` (ARC strong reference counter, `i32`)
    - Index `2`: `weak_count` (ARC weak reference counter, `i32`)
    - Index `3..N`: Parent fields in exact inheritance order
    - Index `N+1..M`: Derived fields
- **Field Shadowing:** Derived classes MUST NOT silently overwrite parent field dictionary indices without adjusting struct offset mapping.
- **VTable Dispatch vs. Static Call:**
    - Normal method call (`this.Foo()` / `obj.Foo()`): Load VTable pointer from struct slot `0` -> GEP method slot -> indirect `BuildCall2`.
    - Base call (`base.Foo()` or `base()` - *Planned feature*): Direct static `BuildCall2` to resolved parent function symbol with `this` bitcasted to `Parent*`. NEVER route `base` through VTable.
- **BasicBlock Hygiene:** Always check `InsertBlock.Terminator.Handle != IntPtr.Zero` before emitting trailing `BuildBr` instructions. Never emit instructions into a terminated block.

## 4. C# & LLVMSHARP CONVENTIONS
- Use primary constructors for CodeGen handlers: `public class FooGen(CodeGenContext ctx)`.
- Throw language-level errors via `new BifyTypeError("...").Throw()` or `return new BifyTypeError("...").Throw<T>()`. Never use C# `throw new` on `BifyError` derivatives (they do not inherit from `System.Exception`).

## 5. COMMIT MESSAGE CONVENTIONS
Follow the repository's established commit style:
- Use a bulleted list where each line begins with a dash `-` followed immediately by a capitalized action verb in past tense (e.g. `-Added ...`, `-Refactored ...`, `-Fixed ...`, `-Moved ...`, `-Implemented ...`).
- Separate distinct changes into individual `-` bullet lines.
- Be concise and focus directly on the subsystem or capability affected.
- Example:
  ```
  -Refactored ControlFlowGen class into separate statements and emitters
  -Implemented two-pass ClassLayoutBuilder for class struct layout and VTable inheritance
  -Added tests for class layout and method override validation
  ```