# Il2CppDumper

[![Build status](https://ci.appveyor.com/api/projects/status/anhqw33vcpmp8ofa?svg=true)](https://ci.appveyor.com/project/Perfare/il2cppdumper/branch/master/artifacts)

## 변경 사항

이 저장소는 기본 Il2CppDumper의 동작을 유지하면서 CodeRegistration과 설정 처리
부분을 보완한 버전입니다.

### CodeRegistration 레이아웃 처리

IL2CPP 버전에 따라 `Il2CppCodeRegistration`에 다음 필드가 포함되지 않는 빌드가
있을 수 있습니다.

* `unresolvedInstanceCallPointers`
* `unresolvedStaticCallPointers`

기존 구현은 이 필드가 항상 존재한다고 가정하고 구조체를 읽었습니다. 실제 빌드에서
필드가 빠져 있으면 그 뒤의 `interopData`, `windowsRuntimeFactory`,
`codeGenModulesCount`, `codeGenModules`가 잘못된 위치에서 읽힐 수 있습니다.

현재 구현은 29.x 및 31.x 계열에서 두 레이아웃을 모두 후보로 읽은 뒤,
`codeGenModules` 포인터와 `codeGenModulesCount`가 실제 파일 범위에 맞는지 검증하여
레이아웃을 선택합니다. 필요한 경우에만 구조체 경계 차이에 해당하는 주소 보정을
사용하며, 특정 게임의 주소나 Count를 소스 코드에 고정하지 않습니다.

메타데이터 버전에서 판별되는 29/29.1, 27.x, 24.x 호환성 처리는 기존 동작을
유지합니다. 버전 31 처리도 Count를 임의의 정답으로 사용하지 않고, 버전 판별과
CodeRegistration 레이아웃 검증을 분리하여 수행합니다.

### Count 검증과 설정 Override

잘못 읽은 Count를 무조건 0으로 바꾸던 `SafeCount()` 방식은 제거했습니다.
`genericMethodTableCount`, `genericInstsCount`, `methodSpecsCount`, `typesCount`,
`codeGenModulesCount`, `genericMethodPointersCount`, `invokerPointersCount`는
각각 대응하는 포인터와 파일 범위에 맞는지 검증합니다. 구조체 레이아웃이나 포인터가
잘못된 경우에는 오류를 명확히 보고하며, 데이터를 0으로 바꾸어 덤프를 강행하지
않습니다.

수동 Override 기능은 유지하되 실제 IL2CPP 필드 이름에 맞게 설정 이름을 변경했습니다.

| 설정 이름 | Override 대상 |
| --- | --- |
| `CustomGenericMethodTableCount` | `genericMethodTableCount` |
| `CustomGenericMethodTable` | `genericMethodTable` |
| `CustomCodeGenModulesCount` | `codeGenModulesCount` |
| `CustomCodeGenModules` | `codeGenModules` |

값을 지정하지 않으면 기본 자동 탐색 결과를 사용합니다. 따라서 임의의 Count를
기본 설정에 넣어 제네릭 메서드나 CodeGen 모듈을 생략하는 방식이 아니라, 올바른
CodeRegistration과 MetadataRegistration을 읽어 전체 데이터를 처리하는 것을
우선합니다.

Unity il2cpp reverse engineer

## Features

* Complete DLL restore (except code), can be used to extract `MonoBehaviour` and `MonoScript`
* Supports ELF, ELF64, Mach-O, PE, NSO and WASM format
* Supports Unity 5.3 - 2022.2
* Supports generate IDA, Ghidra and Binary Ninja scripts to help them better analyze il2cpp files
* Supports generate structures header file
* Supports Android memory dumped `libil2cpp.so` file to bypass protection
* Support bypassing simple PE protection

## Usage

Run `Il2CppDumper.exe` and choose the il2cpp executable file and `global-metadata.dat` file, then enter the information as prompted

The program will then generate all the output files in current working directory

### Command-line

```
Il2CppDumper.exe <executable-file> <global-metadata> <output-directory>
```

### Outputs

#### DummyDll

Folder, containing all restored dll files

Use [dnSpy](https://github.com/0xd4d/dnSpy), [ILSpy](https://github.com/icsharpcode/ILSpy) or other .Net decompiler tools to view

Can be used to extract Unity `MonoBehaviour` and `MonoScript`, for [UtinyRipper](https://github.com/mafaca/UtinyRipper), [UABE](https://7daystodie.com/forums/showthread.php?22675-Unity-Assets-Bundle-Extractor)

#### ida.py

For IDA

#### ida_with_struct.py

For IDA, read il2cpp.h file and apply structure information in IDA

#### il2cpp.h

structure information header file

#### ghidra.py

For Ghidra

#### Il2CppBinaryNinja

For BinaryNinja

#### ghidra_wasm.py

For Ghidra, work with [ghidra-wasm-plugin](https://github.com/nneonneo/ghidra-wasm-plugin)

#### script.json

For ida.py, ghidra.py and Il2CppBinaryNinja

#### stringliteral.json

Contains all stringLiteral information

### Configuration

All the configuration options are located in `config.json`

Available options:

* `DumpMethod`, `DumpField`, `DumpProperty`, `DumpAttribute`, `DumpFieldOffset`, `DumpMethodOffset`, `DumpTypeDefIndex`
  * Whether to output these information to dump.cs

* `GenerateDummyDll`, `GenerateScript`
  * Whether to generate these things

* `DummyDllAddToken`
  * Whether to add token in DummyDll

* `RequireAnyKey`
  * Whether to press any key to exit at the end

* `ForceIl2CppVersion`, `ForceVersion`
  * If `ForceIl2CppVersion` is `true`, the program will use the version number specified in `ForceVersion` to choose parser for il2cpp binaries (does not affect the choice of metadata parser). This may be useful on some older il2cpp version (e.g. the program may need to use v16 parser on il2cpp v20 (Android) binaries in order to work properly)

* `ForceDump`
  * Force files to be treated as dumped

* `NoRedirectedPointer`
  * Treat pointers in dumped files as unredirected, This option needs to be `true` for files dumped from some devices

## Common errors

#### `ERROR: Metadata file supplied is not valid metadata file.`  

Make sure you choose the correct file. Sometimes games may obfuscate this file for content protection purposes and so on. Deobfuscating of such files is beyond the scope of this program, so please **DO NOT** file an issue regarding to deobfuscating.

If your file is `libil2cpp.so` and you have a rooted Android phone, you can try my other project [Zygisk-Il2CppDumper](https://github.com/Perfare/Zygisk-Il2CppDumper), it can bypass this protection.

#### `ERROR: Can't use auto mode to process file, try manual mode.`

Please note that the executable file for the PC platform is `GameAssembly.dll` or `*Assembly.dll`

You can open a new issue and upload the file, I will try to solve.

#### `ERROR: This file may be protected.`

Il2CppDumper detected that the executable file has been protected, use `GameGuardian` to dump `libil2cpp.so` from the game memory, then use Il2CppDumper to load and follow the prompts, can bypass most protections.

If you have a rooted Android phone, you can try my other project [Zygisk-Il2CppDumper](https://github.com/Perfare/Zygisk-Il2CppDumper), it can bypass almost all protections.

## Credits

- Jumboperson - [Il2CppDumper](https://github.com/Jumboperson/Il2CppDumper)
