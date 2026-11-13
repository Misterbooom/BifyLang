#nullable enable
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Types;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class StringCompareGen(CodeGenContext ctx)
{
    private readonly CodeGenContext _ctx = ctx;

    public TypedValue Emit(TokenType op, LLVMValueRef left, LLVMValueRef right)
    {
        if (op is not (TokenType.EQ or TokenType.EQEQ or TokenType.NEQ))
            return new BifySyntaxError($"Unsupported string comparison operator: {op}").Throw<TypedValue>();

        var (strcmpFn, strcmpType) = BuiltinFunctions.GetOrDeclareStrcmp(_ctx);
        var cmpResult = _ctx.Builder.BuildCall2(strcmpType, strcmpFn, new[] { left, right }, "strcmp_res");
        var zero = LLVMValueRef.CreateConstInt(BifyType.CreateInt(_ctx).Type, 0, false);

        var predicate = op == TokenType.NEQ ? LLVMIntPredicate.LLVMIntNE : LLVMIntPredicate.LLVMIntEQ;
        var name = op == TokenType.NEQ ? "strneq" : "streq";
        var boolVal = _ctx.Builder.BuildICmp(predicate, cmpResult, zero, name);
        return new TypedValue(boolVal, BifyType.CreateBool(_ctx));
    }
}
