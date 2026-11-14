#nullable enable
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Core.Validators;
using BifyLang.CodeGen.Expressions;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Statements;

public class VariableDeclGen(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public void EmitVariableDecl(AstVarDecl varDecl)
    {
        string varName = varDecl.AssignmentNode.Left.Token.Value;
        using (_ctx.HeapControl.BeginTempScope())
        {
            var (varType, initialValue) = ResolveVariableType(varDecl);

            var alloca = _ctx.BuildEntryAlloca(varType.Type, varName);

            if (!FlagChecker.CheckVariableFlags(varDecl.Flag?.Flags?.Select(i => i.Token.Value) ?? [], out var error))
            {
                new BifySyntaxError(error).Throw();
                return;
            }

            if (varDecl.AssignmentNode.Right is AstNew && varDecl.Flag?.HasFlag("weak") == true)
            {
                new BifyTypeError("Can't use weak flag with new statement").Throw();
                return;
            }

            bool isWeak = varDecl.Flag?.HasFlag("weak") == true;
            bool isConst = varDecl.Flag?.HasFlag("const") == true;

            StoreInitialValue(alloca, varType, initialValue, isWeak);
        
            _ctx.CurrentScope.Define(varName, alloca, varType, isConst, isWeak);
            BifyDebug.Log(_ctx.CurrentScope.ToString());
        }
        
    }

    private (BifyType VarType, TypedValue? InitialValue) ResolveVariableType(AstVarDecl varDecl)
    {
  
            var expressionDispatcher = new ExpressionDispatcher(_ctx);
            if (varDecl.Type is AstIdentifier id && id.Name == "var")
            {
                if (varDecl.AssignmentNode.Right == null)
                    new BifyTypeError("Implicitly-typed variables ('var') must be initialized.").Throw();
            
                var init = expressionDispatcher.EmitExpression(varDecl.AssignmentNode.Right);
                if (init.Type.Name == "void")
                    new BifyTypeError("Cannot declare a variable of type 'void'.").Throw();

                return (init.Type, init);
            }

            var varType = TypeResolver.Resolve(varDecl.Type, _ctx);
            TypedValue? initialValue = null;
            if (varDecl.AssignmentNode.Right != null)
            {
            
                var raw = expressionDispatcher.EmitExpression(varDecl.AssignmentNode.Right);
                initialValue = TypeCaster.ImplicitCast(_ctx, raw, varType);
            }
            return (varType, initialValue);
        
    }

    private void StoreInitialValue(LLVMValueRef alloca, BifyType varType, TypedValue? initialValue, bool isWeak)
    {
        if (initialValue.HasValue)
        {
            var init = initialValue.Value;
            if (varType is ClassType)
            {
                _ctx.HeapControl.BuildArcStoreInit(alloca, init, isWeak);
            }
            else
            {
                _ctx.Builder.BuildStore(init.Value, alloca);
            }
        }
        else if (varType is ClassType)
        {
            _ctx.Builder.BuildStore(LLVMValueRef.CreateConstNull(varType.Type), alloca);
        }
    }
}