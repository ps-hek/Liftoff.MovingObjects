using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Liftoff.MovingObjects.Patcher
{
    public class Patcher
    {
        public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

        private static FieldDefinition AddSerializableField(AssemblyDefinition assembly, TypeDefinition typeDefinition,
            string name, TypeReference typeReference)
        {
            var attr = assembly.MainModule.ImportReference(
                typeof(XmlElementAttribute).GetConstructor(new[] { typeof(string) }));

            var field = new FieldDefinition(name, FieldAttributes.Public, typeReference);

            var customAttr = new CustomAttribute(attr);
            customAttr.ConstructorArguments.Add(
                new CustomAttributeArgument(assembly.MainModule.TypeSystem.String, name));
            field.CustomAttributes.Add(customAttr);
            typeDefinition.Fields.Add(field);
            return field;
        }


        private static TypeDefinition AddSerializableType(AssemblyDefinition assembly, string name)
        {
            var type = new TypeDefinition("", name,
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable,
                assembly.MainModule.TypeSystem.Object);

            const MethodAttributes methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig |
                                                      MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            var method = new MethodDefinition(".ctor", methodAttributes, assembly.MainModule.TypeSystem.Void);
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Call,
                assembly.MainModule.ImportReference(typeof(object).GetConstructor(Type.EmptyTypes))));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            type.Methods.Add(method);

            var customAttr =
                new CustomAttribute(
                    assembly.MainModule.ImportReference(typeof(SerializableAttribute).GetConstructor(Type.EmptyTypes)));
            type.CustomAttributes.Add(customAttr);

            assembly.MainModule.Types.Add(type);
            return type;
        }

        public static void Patch(AssemblyDefinition assembly)
        {
            var vectorType = assembly.MainModule.GetType("SerializableVector3") ??
                             throw new NullReferenceException("SerializableVector3");

            var animationOptsType = AddSerializableType(assembly, "MO_AnimationOptions");
            AddSerializableField(assembly, animationOptsType, "teleportToStart",
                assembly.MainModule.ImportReference(typeof(bool)));
            AddSerializableField(assembly, animationOptsType, "simulatePhysics",
                assembly.MainModule.ImportReference(typeof(bool)));
            AddSerializableField(assembly, animationOptsType, "simulatePhysicsTime",
                assembly.MainModule.ImportReference(typeof(float)));
            AddSerializableField(assembly, animationOptsType, "simulatePhysicsDelay",
                assembly.MainModule.ImportReference(typeof(float)));
            AddSerializableField(assembly, animationOptsType, "simulatePhysicsWarmupDelay",
                assembly.MainModule.ImportReference(typeof(float)));
            AddSerializableField(assembly, animationOptsType, "animationWarmupDelay",
                assembly.MainModule.ImportReference(typeof(float)));

            AddSerializableField(assembly, animationOptsType, "animationRepeats",
                assembly.MainModule.ImportReference(typeof(int)));


            var triggerType = AddSerializableType(assembly, "MO_TriggerOptions");

            AddSerializableField(assembly, triggerType, "triggerName",
                assembly.MainModule.ImportReference(typeof(string)));
            AddSerializableField(assembly, triggerType, "triggerTarget",
                assembly.MainModule.ImportReference(typeof(string)));
            AddSerializableField(assembly, triggerType, "triggerTeleport",
                assembly.MainModule.ImportReference(typeof(bool)));
            AddSerializableField(assembly, triggerType, "triggerMinSpeed",
                assembly.MainModule.ImportReference(typeof(float)));
            AddSerializableField(assembly, triggerType, "triggerMaxSpeed",
                assembly.MainModule.ImportReference(typeof(float)));

            var animationType = AddSerializableType(assembly, "MO_Animation");
            AddSerializableField(assembly, animationType, "delay", assembly.MainModule.ImportReference(typeof(float)));
            AddSerializableField(assembly, animationType, "time", assembly.MainModule.ImportReference(typeof(float)));

            AddSerializableField(assembly, animationType, "position", vectorType);
            AddSerializableField(assembly, animationType, "rotation", vectorType);

            var trackBlueprintType = assembly.MainModule.GetType("TrackBlueprint");
            AddSerializableField(assembly, trackBlueprintType, "mo_animationSteps",
                assembly.MainModule.ImportReference(typeof(List<>)).MakeGenericInstanceType(animationType));
            AddSerializableField(assembly, trackBlueprintType, "mo_animationOptions", animationOptsType);
            AddSerializableField(assembly, trackBlueprintType, "mo_triggerOptions", triggerType);
            AddSerializableField(assembly, trackBlueprintType, "mo_groupId",
                assembly.MainModule.ImportReference(typeof(string)));
        }
    }
}