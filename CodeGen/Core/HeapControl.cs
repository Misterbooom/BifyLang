using System;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core;

public class HeapControl(CodeGenContext codeGenContext)
{
    private readonly CodeGenContext _ctx = codeGenContext;

    private (LLVMTypeRef funcType, LLVMValueRef funcValue) _gcAlloc;
    private (LLVMTypeRef funcType, LLVMValueRef funcValue) _gcRetainStrong;
    private (LLVMTypeRef funcType, LLVMValueRef funcValue) _gcRetainWeak;
    private (LLVMTypeRef funcType, LLVMValueRef funcValue) _gcReleaseStrong;
    private (LLVMTypeRef funcType, LLVMValueRef funcValue) _gcReleaseWeak;
    private (LLVMTypeRef funcType, LLVMValueRef funcValue) _gcUpgradeWeak;

    private LLVMTypeRef VoidPtrType => LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0);

    public void DefineRuntimeFunctions()
    {
        DefineGcRetainStrong();
        DefineGcRetainWeak();
        DefineGcReleaseStrong();
        DefineGcReleaseWeak();
        DefineGcUpgradeWeak();
    }


    private void DefineGcRetainStrong()
    {
        LLVMTypeRef[] paramTypes = [VoidPtrType];
        var funcType = LLVMTypeRef.CreateFunction(_ctx.Context.VoidType, paramTypes);
        var funcVal = _ctx.Module.AddFunction("gc_retainStrong", funcType);
        _gcRetainStrong = (funcType, funcVal);
    }

    private void DefineGcRetainWeak()
    {
        LLVMTypeRef[] paramTypes = [VoidPtrType];
        var funcType = LLVMTypeRef.CreateFunction(_ctx.Context.VoidType, paramTypes);
        var funcVal = _ctx.Module.AddFunction("gc_retainWeak", funcType);
        _gcRetainWeak = (funcType, funcVal);
    }

    private void DefineGcReleaseStrong()
    {
        LLVMTypeRef[] paramTypes = [VoidPtrType];
        var funcType = LLVMTypeRef.CreateFunction(_ctx.Context.VoidType, paramTypes);
        var funcVal = _ctx.Module.AddFunction("gc_releaseStrong", funcType);
        _gcReleaseStrong = (funcType, funcVal);
    }

    private void DefineGcReleaseWeak()
    {
        LLVMTypeRef[] paramTypes = [VoidPtrType];
        var funcType = LLVMTypeRef.CreateFunction(_ctx.Context.VoidType, paramTypes);
        var funcVal = _ctx.Module.AddFunction("gc_releaseWeak", funcType);
        _gcReleaseWeak = (funcType, funcVal);
    }

    private void DefineGcUpgradeWeak()
    {
        LLVMTypeRef[] paramTypes = [VoidPtrType];
        var funcType = LLVMTypeRef.CreateFunction(VoidPtrType, paramTypes);
        var funcVal = _ctx.Module.AddFunction("gc_upgradeWeak", funcType);
        _gcUpgradeWeak = (funcType, funcVal);
    }


    public void BuildRetainStrong(LLVMValueRef objPtr)
    {
        LLVMValueRef[] args = [objPtr];
        _ctx.Builder.BuildCall2(_gcRetainStrong.funcType, _gcRetainStrong.funcValue, args);
    }

    public void BuildRetainWeak(LLVMValueRef objPtr)
    {
        LLVMValueRef[] args = [objPtr];
        _ctx.Builder.BuildCall2(_gcRetainWeak.funcType, _gcRetainWeak.funcValue, args);
    }

    public void BuildReleaseStrong(LLVMValueRef objPtr)
    {
        LLVMValueRef[] args = [objPtr];
        _ctx.Builder.BuildCall2(_gcReleaseStrong.funcType, _gcReleaseStrong.funcValue, args);
    }

    public void BuildReleaseWeak(LLVMValueRef objPtr)
    {
        LLVMValueRef[] args = [objPtr];
        _ctx.Builder.BuildCall2(_gcReleaseWeak.funcType, _gcReleaseWeak.funcValue, args);
    }

    public LLVMValueRef BuildUpgradeWeak(LLVMValueRef weakPtr)
    {
        LLVMValueRef[] args = [weakPtr];
        return _ctx.Builder.BuildCall2(_gcUpgradeWeak.funcType, _gcUpgradeWeak.funcValue, args, "upgraded_ptr");
    }
}