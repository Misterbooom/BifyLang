using System;
using System.IO;
using LLVMSharp;
using LLVMSharp.Interop;
using BifyLang.Ast;
using BifyLang.Exceptions;
using BifyLang.CodeGen.Core;

namespace BifyLang.CodeGen
{
    [Flags]
    internal enum CompilerFlags
    {
        NONE, 
        EXECUTE = 1 << 0
    }

    internal class BifyCompiler
    {
        public BifyCompiler()
        {
            LLVM.InitializeAllTargetInfos();
            LLVM.InitializeAllTargets();
            LLVM.InitializeAllTargetMCs();
            LLVM.InitializeAllAsmParsers();
            LLVM.InitializeAllAsmPrinters();
        }

        public static bool VerifyModule(LLVMModuleRef module, out string? error)
        {
            error = null;
            unsafe
            {
                sbyte* message = null; 

                if (LLVM.VerifyModule(
                        module, 
                        LLVMVerifierFailureAction.LLVMReturnStatusAction, 
                        &message) != 0)
                {
                    if (message != null)
                    {
                        error = new string(message);
                        LLVM.DisposeMessage(message);
                    }
                    return false;
                }
            }
            

            return true;
        }
        public void Compile(AstModule moduleNode, string filePath, string outputFilePath, CompilerFlags flags)
        {
            // LLVMValueRef mainFunction = Module.GetNamedFunction("main");
            var module = new CodeGenContext().GenerateModule(moduleNode);
            
            LLVMValueRef mainFunction = module.GetNamedFunction("main");
            if (mainFunction.Handle == IntPtr.Zero)
            {
                // Console.WriteLine("Main function not found.");
                new BifySyntaxError("Main function not found.").Throw();
                return;
            }
            if (!VerifyModule(module, out var error))
            {
                Console.WriteLine($"LLVM module verification failed:\n{error}");
                Console.WriteLine(module.ToString());
            }

            string fileExtension = Path.GetExtension(outputFilePath);
            if (fileExtension == ".o")
            {
                GenerateObjectFile(filePath, outputFilePath, module);
            }
            else if (fileExtension is ".out" or "")
            {
                GenerateExecutable(filePath, outputFilePath, flags, module);
            }
            else if (fileExtension == ".ir")
            {
                File.WriteAllText(outputFilePath,module.ToString());
            }
            else
            {
                Console.WriteLine($"Invalid output file extension: {fileExtension}");
            }
        }

        private void GenerateObjectFile(string filePath, string outputFilePath, LLVMModuleRef module)
        {
            unsafe
            {
                try
                {
                    LLVMTargetMachineRef targetMachine = CreateTargetMachine();
                    targetMachine.EmitToFile(module, outputFilePath, LLVMCodeGenFileType.LLVMObjectFile);
                    LLVM.DisposeTargetMachine(targetMachine);
                }
                catch (Exception e)
                {
                    BifyDebug.Log($"Failed to create target machine: {e.Message}");
                }

            }
        }

        // TODO: Add windows support
        private void GenerateExecutable(string filePath, string outputFilePath, CompilerFlags flags, LLVMModuleRef module)
        {
            string tempObjFile = Path.GetTempFileName() + ".o";
            BifyDebug.Log("Generating object file:"  + tempObjFile);
            GenerateObjectFile(filePath, tempObjFile, module);
            BifyDebug.Log("Compiling object file: " + tempObjFile);
            LinkLibraries(tempObjFile, outputFilePath);
            
            
            if (File.Exists(tempObjFile))
            {
                File.Delete(tempObjFile);
            }

            if (flags.HasFlag(CompilerFlags.EXECUTE))
            {
                BifyDebug.Log("Running executable: " + outputFilePath);
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = outputFilePath,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new System.Diagnostics.Process { StartInfo = processInfo };

                process.Start();
    
    
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    BifyDebug.Log($"Process ended up with error. Code: {process.ExitCode}");
                }
            }
        }

        private void LinkLibraries(string objectFilePath, string outputFilePath)
        {
            string stdcPath = "/home/artur/RiderProjects/BifyLang/CodeGen/runtime.c";
            
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "clang",
                Arguments = $"\"{objectFilePath}\" \"{stdcPath}\" -o \"{outputFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processInfo);
            process?.WaitForExit();

            if (process != null && process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
                throw new InvalidOperationException($"Clang compilation and linking failed:\n{error}");
            }
            Console.WriteLine("Successfully compiled.");
        }

        private unsafe LLVMTargetMachineRef CreateTargetMachine()
        {
            sbyte* targetTriple = LLVM.GetDefaultTargetTriple();
            LLVMTarget* target;
            sbyte* error = null;

            if (LLVM.GetTargetFromTriple(targetTriple, &target, &error) != 0)
            {
                string message = new string(error);
                LLVM.DisposeMessage(error);
                throw new InvalidOperationException($"Error selecting target: {message}");
            }

            LLVMTargetMachineRef targetMachine;

            fixed (byte* cpuPtr = "generic\0"u8)
            fixed (byte* featuresPtr = "\0"u8)
            {
                targetMachine = LLVM.CreateTargetMachine(
                    target,
                    targetTriple,
                    (sbyte*)cpuPtr,
                    (sbyte*)featuresPtr,
                    LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault,
                    LLVMRelocMode.LLVMRelocDefault,
                    LLVMCodeModel.LLVMCodeModelDefault
                );
            }

            return targetMachine;
        }

    }
}