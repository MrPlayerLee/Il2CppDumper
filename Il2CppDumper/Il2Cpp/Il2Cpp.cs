using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Il2CppDumper
{
    public abstract class Il2Cpp : BinaryStream
    {
        private Il2CppMetadataRegistration pMetadataRegistration;
        private Il2CppCodeRegistration pCodeRegistration;
        public ulong[] methodPointers;
        public ulong[] genericMethodPointers;
        public ulong[] invokerPointers;
        public ulong[] customAttributeGenerators;
        public ulong[] reversePInvokeWrappers;
        public ulong[] unresolvedVirtualCallPointers;
        private ulong[] fieldOffsets;
        public Il2CppType[] types;
        private readonly Dictionary<ulong, Il2CppType> typeDic = new();
        public ulong[] metadataUsages;
        private Il2CppGenericMethodFunctionsDefinitions[] genericMethodTable;
        public ulong[] genericInstPointers;
        public Il2CppGenericInst[] genericInsts;
        public Il2CppMethodSpec[] methodSpecs;
        public Dictionary<int, List<Il2CppMethodSpec>> methodDefinitionMethodSpecs = new();
        public Dictionary<Il2CppMethodSpec, ulong> methodSpecGenericMethodPointers = new();
        private bool fieldOffsetsArePointers;
        protected long metadataUsagesCount;
        public Dictionary<string, Il2CppCodeGenModule> codeGenModules;
        public Dictionary<string, ulong[]> codeGenModuleMethodPointers;
        public Dictionary<string, Dictionary<uint, Il2CppRGCTXDefinition[]>> rgctxsDictionary;
        public bool IsDumped;

        public abstract ulong MapVATR(ulong addr);
        public abstract ulong MapRTVA(ulong addr);
        public abstract bool Search();
        public abstract bool PlusSearch(int methodCount, int typeDefinitionsCount, int imageCount);
        public abstract bool SymbolSearch();
        public abstract SectionHelper GetSectionHelper(int methodCount, int typeDefinitionsCount, int imageCount);
        public abstract bool CheckDump();

        protected Il2Cpp(Stream stream) : base(stream) { }

        public void SetProperties(double version, long metadataUsagesCount)
        {
            Version = version;
            this.metadataUsagesCount = metadataUsagesCount;
        }

        protected bool AutoPlusInit(ulong codeRegistration, ulong metadataRegistration)
        {
            if (codeRegistration != 0)
            {
                var limit = this is WebAssemblyMemory ? 0x35000u : 0x50000u;
                if (Version >= 24.2)
                {
                    pCodeRegistration = MapVATR<Il2CppCodeRegistration>(codeRegistration);
                    if (Version == 31)
                    {
                        if (pCodeRegistration.genericMethodPointersCount > limit)
                        {
                            codeRegistration -= PointerSize * 2;
                        }
                        else
                        {
                            Version = 29;
                            Console.WriteLine($"Change il2cpp version to: {Version}");
                        }
                    }
                    if (Version == 29)
                    {
                        if (pCodeRegistration.genericMethodPointersCount > limit)
                        {
                            Version = 29.1;
                            codeRegistration -= PointerSize * 2;
                            Console.WriteLine($"Change il2cpp version to: {Version}");
                        }
                    }
                    if (Version == 27)
                    {
                        if (pCodeRegistration.reversePInvokeWrapperCount > limit)
                        {
                            Version = 27.1;
                            codeRegistration -= PointerSize;
                            Console.WriteLine($"Change il2cpp version to: {Version}");
                        }
                    }
                    if (Version == 24.4)
                    {
                        codeRegistration -= PointerSize * 2;
                        if (pCodeRegistration.reversePInvokeWrapperCount > limit)
                        {
                            Version = 24.5;
                            codeRegistration -= PointerSize;
                            Console.WriteLine($"Change il2cpp version to: {Version}");
                        }
                    }
                    if (Version == 24.2)
                    {
                        if (pCodeRegistration.interopDataCount == 0)
                        {
                            Version = 24.3;
                            codeRegistration -= PointerSize * 2;
                            Console.WriteLine($"Change il2cpp version to: {Version}");
                        }
                    }
                }
            }
            Console.WriteLine("CodeRegistration : {0:x}", codeRegistration);
            Console.WriteLine("MetadataRegistration : {0:x}", metadataRegistration);
            if (codeRegistration != 0 && metadataRegistration != 0)
            {
                Init(codeRegistration, metadataRegistration);
                return true;
            }
            return false;
        }

        public virtual void Init(ulong codeRegistration, ulong metadataRegistration)
        {
            pCodeRegistration = MapVATR<Il2CppCodeRegistration>(codeRegistration);
            var limit = this is WebAssemblyMemory ? 0x35000u : 0x50000u;
            if (Version == 27 && pCodeRegistration.invokerPointersCount > limit)
            {
                Version = 27.1;
                Console.WriteLine($"Change il2cpp version to: {Version}");
                pCodeRegistration = MapVATR<Il2CppCodeRegistration>(codeRegistration);
            }
            if (Version == 27.1)
            {
                var pCodeGenModules = MapVATR<ulong>(pCodeRegistration.codeGenModules, pCodeRegistration.codeGenModulesCount);
                foreach (var pCodeGenModule in pCodeGenModules)
                {
                    var codeGenModule = MapVATR<Il2CppCodeGenModule>(pCodeGenModule);
                    if (codeGenModule.rgctxsCount > 0)
                    {
                        var rgctxs = MapVATR<Il2CppRGCTXDefinition>(codeGenModule.rgctxs, codeGenModule.rgctxsCount);
                        if (rgctxs.All(x => x.data.rgctxDataDummy > limit))
                        {
                            Version = 27.2;
                            Console.WriteLine($"Change il2cpp version to: {Version}");
                        }
                        break;
                    }
                }
            }
            if (Version == 24.4 && pCodeRegistration.invokerPointersCount > limit)
            {
                Version = 24.5;
                Console.WriteLine($"Change il2cpp version to: {Version}");
                pCodeRegistration = MapVATR<Il2CppCodeRegistration>(codeRegistration);
            }
            if (Version == 24.2 && pCodeRegistration.codeGenModules == 0)
            {
                Version = 24.3;
                Console.WriteLine($"Change il2cpp version to: {Version}");
                pCodeRegistration = MapVATR<Il2CppCodeRegistration>(codeRegistration);
            }

            pMetadataRegistration = MapVATR<Il2CppMetadataRegistration>(metadataRegistration);

            // [!] 안티 덤프 우회: 잘못된 주소 참조로 인한 Overflow 방지 (SafeCount 적용)
            pCodeRegistration.genericMethodPointersCount = SafeCount(pCodeRegistration.genericMethodPointersCount);
            pCodeRegistration.invokerPointersCount = SafeCount(pCodeRegistration.invokerPointersCount);
            if (Version < 27) pCodeRegistration.customAttributeCount = SafeCount(pCodeRegistration.customAttributeCount);
            if (Version >= 22)
            {
                pCodeRegistration.reversePInvokeWrapperCount = SafeCount(pCodeRegistration.reversePInvokeWrapperCount);
                pCodeRegistration.unresolvedVirtualCallCount = SafeCount(pCodeRegistration.unresolvedVirtualCallCount);
            }
            pMetadataRegistration.genericInstsCount = SafeCount(pMetadataRegistration.genericInstsCount);
            pMetadataRegistration.fieldOffsetsCount = SafeCount(pMetadataRegistration.fieldOffsetsCount);
            pMetadataRegistration.typesCount = SafeCount(pMetadataRegistration.typesCount);
            pMetadataRegistration.methodSpecsCount = SafeCount(pMetadataRegistration.methodSpecsCount);
            pMetadataRegistration.genericMethodTableCount = SafeCount(pMetadataRegistration.genericMethodTableCount);
            if (Version >= 24.2) pCodeRegistration.codeGenModulesCount = SafeCount(pCodeRegistration.codeGenModulesCount);

            // [!] Config 오버라이드 로직 적용
            if (Program.config.CustomMetadataCount > 0)
            {
                pMetadataRegistration.genericMethodTableCount = Program.config.CustomMetadataCount;
                Console.WriteLine($"[!] Metadata Count Overridden: {pMetadataRegistration.genericMethodTableCount}");
            }
            if (!string.IsNullOrEmpty(Program.config.CustomMetadataTable))
            {
                pMetadataRegistration.genericMethodTable = Convert.ToUInt64(Program.config.CustomMetadataTable, 16);
                Console.WriteLine($"[!] Metadata Table Overridden: 0x{pMetadataRegistration.genericMethodTable:X}");
            }

            if (Program.config.CustomCodeCount > 0)
            {
                pCodeRegistration.codeGenModulesCount = (ulong)Program.config.CustomCodeCount;
                Console.WriteLine($"[!] Code Count Overridden: {pCodeRegistration.codeGenModulesCount}");
            }
            if (!string.IsNullOrEmpty(Program.config.CustomCodeTable))
            {
                pCodeRegistration.codeGenModules = Convert.ToUInt64(Program.config.CustomCodeTable, 16);
                Console.WriteLine($"[!] Code Table Overridden: 0x{pCodeRegistration.codeGenModules:X}");
            }

            genericMethodPointers = MapVATR<ulong>(pCodeRegistration.genericMethodPointers, pCodeRegistration.genericMethodPointersCount);
            invokerPointers = MapVATR<ulong>(pCodeRegistration.invokerPointers, pCodeRegistration.invokerPointersCount);
            if (Version < 27)
            {
                customAttributeGenerators = MapVATR<ulong>(pCodeRegistration.customAttributeGenerators, pCodeRegistration.customAttributeCount);
            }
            if (Version > 16 && Version < 27)
            {
                metadataUsages = MapVATR<ulong>(pMetadataRegistration.metadataUsages, metadataUsagesCount);
            }
            if (Version >= 22)
            {
                if (pCodeRegistration.reversePInvokeWrapperCount != 0)
                    reversePInvokeWrappers = MapVATR<ulong>(pCodeRegistration.reversePInvokeWrappers, pCodeRegistration.reversePInvokeWrapperCount);
                if (pCodeRegistration.unresolvedVirtualCallCount != 0)
                    unresolvedVirtualCallPointers = MapVATR<ulong>(pCodeRegistration.unresolvedVirtualCallPointers, pCodeRegistration.unresolvedVirtualCallCount);
            }
            genericInstPointers = MapVATR<ulong>(pMetadataRegistration.genericInsts, pMetadataRegistration.genericInstsCount);
            genericInsts = Array.ConvertAll(genericInstPointers, MapVATR<Il2CppGenericInst>);
            fieldOffsetsArePointers = Version > 21;
            if (Version == 21)
            {
                var fieldTest = MapVATR<uint>(pMetadataRegistration.fieldOffsets, 6);
                fieldOffsetsArePointers = fieldTest[0] == 0 && fieldTest[1] == 0 && fieldTest[2] == 0 && fieldTest[3] == 0 && fieldTest[4] == 0 && fieldTest[5] > 0;
            }
            if (fieldOffsetsArePointers)
            {
                fieldOffsets = MapVATR<ulong>(pMetadataRegistration.fieldOffsets, pMetadataRegistration.fieldOffsetsCount);
            }
            else
            {
                fieldOffsets = Array.ConvertAll(MapVATR<uint>(pMetadataRegistration.fieldOffsets, pMetadataRegistration.fieldOffsetsCount), x => (ulong)x);
            }
            var pTypes = MapVATR<ulong>(pMetadataRegistration.types, pMetadataRegistration.typesCount);
            types = new Il2CppType[pMetadataRegistration.typesCount];
            for (var i = 0; i < pMetadataRegistration.typesCount; ++i)
            {
                types[i] = MapVATR<Il2CppType>(pTypes[i]);
                types[i].Init(Version);
                typeDic.Add(pTypes[i], types[i]);
            }
            if (Version >= 24.2)
            {
                var pCodeGenModules = MapVATR<ulong>(pCodeRegistration.codeGenModules, pCodeRegistration.codeGenModulesCount);
                codeGenModules = new Dictionary<string, Il2CppCodeGenModule>(pCodeGenModules.Length, StringComparer.Ordinal);
                codeGenModuleMethodPointers = new Dictionary<string, ulong[]>(pCodeGenModules.Length, StringComparer.Ordinal);
                rgctxsDictionary = new Dictionary<string, Dictionary<uint, Il2CppRGCTXDefinition[]>>(pCodeGenModules.Length, StringComparer.Ordinal);
                foreach (var pCodeGenModule in pCodeGenModules)
                {
                    var codeGenModule = MapVATR<Il2CppCodeGenModule>(pCodeGenModule);
                    var moduleName = ReadStringToNull(MapVATR(codeGenModule.moduleName));
                    codeGenModules.Add(moduleName, codeGenModule);
                    ulong[] methodPointers;
                    try
                    {
                        methodPointers = MapVATR<ulong>(codeGenModule.methodPointers, codeGenModule.methodPointerCount);
                    }
                    catch
                    {
                        methodPointers = new ulong[codeGenModule.methodPointerCount];
                    }
                    codeGenModuleMethodPointers.Add(moduleName, methodPointers);

                    var rgctxsDefDictionary = new Dictionary<uint, Il2CppRGCTXDefinition[]>();
                    rgctxsDictionary.Add(moduleName, rgctxsDefDictionary);
                    if (codeGenModule.rgctxsCount > 0)
                    {
                        var rgctxs = MapVATR<Il2CppRGCTXDefinition>(codeGenModule.rgctxs, codeGenModule.rgctxsCount);
                        var rgctxRanges = MapVATR<Il2CppTokenRangePair>(codeGenModule.rgctxRanges, codeGenModule.rgctxRangesCount);
                        foreach (var rgctxRange in rgctxRanges)
                        {
                            var rgctxDefs = new Il2CppRGCTXDefinition[rgctxRange.range.length];
                            Array.Copy(rgctxs, rgctxRange.range.start, rgctxDefs, 0, rgctxRange.range.length);
                            rgctxsDefDictionary.Add(rgctxRange.token, rgctxDefs);
                        }
                    }
                }
            }
            else
            {
                methodPointers = MapVATR<ulong>(pCodeRegistration.methodPointers, pCodeRegistration.methodPointersCount);
            }
            genericMethodTable = MapVATR<Il2CppGenericMethodFunctionsDefinitions>(pMetadataRegistration.genericMethodTable, pMetadataRegistration.genericMethodTableCount);
            methodSpecs = MapVATR<Il2CppMethodSpec>(pMetadataRegistration.methodSpecs, pMetadataRegistration.methodSpecsCount);
            foreach (var table in genericMethodTable)
            {
                var methodSpec = methodSpecs[table.genericMethodIndex];
                var methodDefinitionIndex = methodSpec.methodDefinitionIndex;
                if (!methodDefinitionMethodSpecs.TryGetValue(methodDefinitionIndex, out var list))
                {
                    list = new List<Il2CppMethodSpec>();
                    methodDefinitionMethodSpecs.Add(methodDefinitionIndex, list);
                }
                list.Add(methodSpec);
                methodSpecGenericMethodPointers.Add(methodSpec, genericMethodPointers[table.indices.methodIndex]);
            }
        }

        public T MapVATR<T>(ulong addr) where T : new()
        {
            return ReadClass<T>(MapVATR(addr));
        }

        public T[] MapVATR<T>(ulong addr, ulong count) where T : new()
        {
            return ReadClassArray<T>(MapVATR(addr), count);
        }

        public T[] MapVATR<T>(ulong addr, long count) where T : new()
        {
            return ReadClassArray<T>(MapVATR(addr), count);
        }

        public int GetFieldOffsetFromIndex(int typeIndex, int fieldIndexInType, int fieldIndex, bool isValueType, bool isStatic)
        {
            try
            {
                var offset = -1;
                if (fieldOffsetsArePointers)
                {
                    var ptr = fieldOffsets[typeIndex];
                    if (ptr > 0)
                    {
                        Position = MapVATR(ptr) + 4ul * (ulong)fieldIndexInType;
                        offset = ReadInt32();
                    }
                }
                else
                {
                    offset = (int)fieldOffsets[fieldIndex];
                }
                if (offset > 0)
                {
                    if (isValueType && !isStatic)
                    {
                        if (Is32Bit)
                        {
                            offset -= 8;
                        }
                        else
                        {
                            offset -= 16;
                        }
                    }
                }
                return offset;
            }
            catch
            {
                return -1;
            }
        }

        public Il2CppType GetIl2CppType(ulong pointer)
        {
            if (!typeDic.TryGetValue(pointer, out var type))
            {
                return null;
            }
            return type;
        }

        public ulong GetMethodPointer(string imageName, Il2CppMethodDefinition methodDef)
        {
            if (Version >= 24.2)
            {
                var methodToken = methodDef.token;
                // [!] KeyNotFoundException 방지를 위해 TryGetValue 사용 및 null 체크 추가
                if (codeGenModuleMethodPointers != null && codeGenModuleMethodPointers.TryGetValue(imageName, out var ptrs))
                {
                    var methodPointerIndex = methodToken & 0x00FFFFFFu;
                    if (ptrs != null && methodPointerIndex > 0 && methodPointerIndex <= (ulong)ptrs.Length)
                    {
                        return ptrs[methodPointerIndex - 1];
                    }
                }
                return 0; // 주소를 찾지 못하면 0을 반환하여 강제 종료 방지
            }
            else
            {
                var methodIndex = methodDef.methodIndex;
                if (methodPointers != null && methodIndex >= 0 && methodIndex < methodPointers.Length)
                {
                    return methodPointers[methodIndex];
                }
            }
            return 0;
        }

        public virtual ulong GetRVA(ulong pointer)
        {
            return pointer;
        }

        // [!] 쓰레기 값 필터링을 위한 안전 도우미 함수 (1600만 이상은 쓰레기 데이터로 간주)
        private ulong SafeCount(ulong count)
        {
            return count > 0x1000000 ? 0 : count;
        }

        private long SafeCount(long count)
        {
            return count < 0 || count > 0x1000000 ? 0 : count;
        }
    }
}