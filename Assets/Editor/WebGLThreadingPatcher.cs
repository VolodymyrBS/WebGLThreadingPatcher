using Mono.Cecil;
using Mono.Cecil.Cil;
using System.IO;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Il2Cpp;
using UnityEngine;

namespace WebGLThreadingPatcher
{
    public class ThreadingPatcher : IIl2CppProcessor
    {
        public int callbackOrder => 0;

        public void OnBeforeConvertRun(
            BuildReport report,
            Il2CppBuildPipelineData data)
        {
            if (data.target != UnityEditor.BuildTarget.WebGL)
                return;

            using (var assembly = AssemblyDefinition.ReadAssembly(Path.Combine(data.inputDirectory, "mscorlib.dll"), new ReaderParameters(ReadingMode.Immediate) { ReadWrite = true }))
            {
                PatchThreadPool(assembly);
                assembly.Write();
            }
        }

        private void PatchThreadPool(AssemblyDefinition assemblyDefinition)
        {
            var mainModule = assemblyDefinition.MainModule;
            if (!TryGetTypes(mainModule, out var threadPool, out var synchronizationContext, out var postCallback, out var waitCallback, out var taskExecutionItem))
                return;

            var taskExecutionCallcack = AddTaskExecutionPostCallback(threadPool, taskExecutionItem, mainModule);

            foreach (var methodDefinition in threadPool.Methods)
            {
                switch (methodDefinition.Name)
                {
                    case "QueueUserWorkItem":
                    case "UnsafeQueueUserWorkItem":
                        PatchQueueUserWorkItem(mainModule, methodDefinition, synchronizationContext, waitCallback, postCallback);
                        break;
                    case "UnsafeQueueCustomWorkItem":
                        PatchUnsafeQueueCustomWorkItem(mainModule, methodDefinition, synchronizationContext, taskExecutionCallcack, postCallback);
                        break;
                    case "TryPopCustomWorkItem":
                        PatchTryPopCustomWorkItem(methodDefinition);
                        break;
                }   
            }
        }

        private void PatchQueueUserWorkItem(ModuleDefinition moduleDefinition, MethodDefinition methodDefinition, TypeDefinition synchronizationContext, TypeDefinition waitCallback, TypeDefinition postCallback)
        {
            var ilPProcessor = methodDefinition.Body.GetILProcessor();
            ilPProcessor.Body.Instructions.Clear();
            methodDefinition.Body.ExceptionHandlers.Clear();
            ilPProcessor.Emit(OpCodes.Call, moduleDefinition.ImportReference(synchronizationContext.Methods.Single(s => s.Name == "get_Current")));
            ilPProcessor.Emit(OpCodes.Ldarg_0);
            ilPProcessor.Emit(OpCodes.Ldftn, moduleDefinition.ImportReference(waitCallback.Methods.Single(s => s.Name == "Invoke")));
            ilPProcessor.Emit(OpCodes.Newobj, moduleDefinition.ImportReference(postCallback.Methods.First(s => s.IsConstructor)));
            if (methodDefinition.Parameters.Count == 2)
                ilPProcessor.Emit(OpCodes.Ldarg_1);
            else
                ilPProcessor.Emit(OpCodes.Ldnull);
            ilPProcessor.Emit(OpCodes.Callvirt, moduleDefinition.ImportReference(synchronizationContext.Methods.Single(s => s.Name == "Post")));

            ilPProcessor.Emit(OpCodes.Ldc_I4_1);
            ilPProcessor.Emit(OpCodes.Ret);
        }

        private void PatchUnsafeQueueCustomWorkItem(ModuleDefinition moduleDefinition, MethodDefinition methodDefinition, TypeDefinition synchronizationContext, MethodDefinition taskExecutionCallcack, TypeDefinition postCallback)
        {
            var p = methodDefinition.Body.GetILProcessor();
            p.Body.Instructions.Clear();
            methodDefinition.Body.ExceptionHandlers.Clear();
            p.Emit(OpCodes.Call, moduleDefinition.ImportReference(synchronizationContext.Methods.Single(s => s.Name == "get_Current")));
            p.Emit(OpCodes.Ldnull);
            p.Emit(OpCodes.Ldftn, moduleDefinition.ImportReference(taskExecutionCallcack));
            p.Emit(OpCodes.Newobj, moduleDefinition.ImportReference(postCallback.Methods.First(s => s.IsConstructor)));
            p.Emit(OpCodes.Ldarg_0);
            p.Emit(OpCodes.Callvirt, moduleDefinition.ImportReference(synchronizationContext.Methods.Single(s => s.Name == "Post")));

            p.Emit(OpCodes.Ret);
        }

        private void PatchTryPopCustomWorkItem(MethodDefinition methodDefinition)
        {
            var ilPProcessor = methodDefinition.Body.GetILProcessor();
            ilPProcessor.Body.Instructions.Clear();
            methodDefinition.Body.ExceptionHandlers.Clear();
            ilPProcessor.Emit(OpCodes.Ldc_I4_0);
            ilPProcessor.Emit(OpCodes.Ret);
        }

        private MethodDefinition AddTaskExecutionPostCallback(TypeDefinition threadPool, TypeDefinition taskExecutionItem, ModuleDefinition moduleDefinition)
        {
            var method = new MethodDefinition("TaskExecutionItemExecute",
                   MethodAttributes.Static | MethodAttributes.Private | MethodAttributes.HideBySig,
                   moduleDefinition.TypeSystem.Void);

            method.Parameters.Add(new ParameterDefinition("state", ParameterAttributes.None, moduleDefinition.TypeSystem.Object));

            var ilProcessor = method.Body.GetILProcessor();
            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Callvirt, moduleDefinition.ImportReference(taskExecutionItem.Methods.Single(s => s.Name == "ExecuteWorkItem")));
            ilProcessor.Emit(OpCodes.Ret);

            threadPool.Methods.Add(method);

            return method;
        }

        private bool TryGetTypes(ModuleDefinition moduleDefinition, out TypeDefinition threadPool, out TypeDefinition synchronizationContext, out TypeDefinition sendOrPostCallback, out TypeDefinition waitCallback, out TypeDefinition threadPoolWorkItem)
        {
            threadPool = null;
            synchronizationContext = null;
            sendOrPostCallback = null;
            waitCallback = null;
            threadPoolWorkItem = null;

            foreach (var type in moduleDefinition.Types)
            {
                if (type.FullName.Contains("System.Threading.ThreadPool"))
                    threadPool = type;
                if (type.FullName.Contains("System.Threading.SynchronizationContext"))
                    synchronizationContext = type;
                if (type.FullName.Contains("System.Threading.SendOrPostCallback"))
                    sendOrPostCallback = type;
                if (type.FullName.Contains("System.Threading.WaitCallback"))
                    waitCallback = type;
                if (type.FullName.Contains("System.Threading.IThreadPoolWorkItem"))
                    threadPoolWorkItem = type;
            }

            return CheckTypeAssigned("System.Threading.ThreadPool", threadPool) &&
                CheckTypeAssigned("System.Threading.SynchronizationContext", synchronizationContext) &&
                CheckTypeAssigned("System.Threading.SendOrPostCallback", sendOrPostCallback) &&
                CheckTypeAssigned("System.Threading.WaitCallback", waitCallback) &&
                CheckTypeAssigned("System.Threading.IThreadPoolWorkItem", threadPoolWorkItem);

            bool CheckTypeAssigned(string name, TypeDefinition type)
            {
                if (type != null)
                    return true;

                Debug.LogError("Can't find " + name);
                return false;
            }
        }
    }
}