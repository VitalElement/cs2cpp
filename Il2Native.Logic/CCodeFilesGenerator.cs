﻿// Mr Oleksandr Duzhar licenses this file to you under the MIT license.
// If you need the License file, please send an email to duzhar@googlemail.com
// 
namespace Il2Native.Logic
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using DOM;
    using DOM.Synthesized;
    using DOM2;
    using Microsoft.CodeAnalysis;
    using Properties;
    using Roslyn.Utilities;

    public class CCodeFilesGenerator
    {
        private string currentFolder;

        public bool Concurrent { get; set; }

        public static void WriteSourceInclude(IndentedTextWriter itw, AssemblyIdentity identity)
        {
            itw.Write("#include \"");
            itw.WriteLine("{0}.h\"", identity.Name);
        }

        public static void WriteSourceMainEntry(CCodeWriterBase c, IndentedTextWriter itw, IMethodSymbol mainMethod)
        {
            itw.WriteLine();
            var mainHasParameters = mainMethod.Parameters.Length > 0;
            if (mainHasParameters)
            {
                itw.Write("auto main(int32_t argc, char* argv[])");
            }
            else
            {
                itw.Write("auto main()");
            }

            itw.WriteLine(" -> int32_t");
            itw.WriteLine("{");
            itw.Indent++;

            itw.WriteLine("atexit(__at_exit);");
            itw.WriteLine("GC_set_all_interior_pointers(1);");
            itw.WriteLine("GC_INIT();");
            if (mainHasParameters)
            {
                itw.WriteLine("auto arguments_count = argc > 0 ? argc - 1 : 0;");
                itw.WriteLine("auto args = __array<string*>::__new_array(arguments_count);");
                itw.WriteLine("for( auto i = 0; i < arguments_count; i++ )");
                itw.WriteLine("{");
                itw.Indent++;
                itw.WriteLine("auto argv1 = argv[i + 1];");
                itw.WriteLine(
                    "args->operator[](i) = string::CreateStringFromEncoding((uint8_t*)argv1, std::strlen(argv1), CoreLib::System::Text::Encoding::get_UTF8());");
                itw.Indent--;
                itw.WriteLine("}");
                itw.WriteLine(string.Empty);
            }

            if (!mainMethod.ReturnsVoid)
            {
                itw.Write("auto exit_code = ");
            }

            c.WriteMethodFullName(mainMethod);
            itw.Write("(");
            if (mainHasParameters)
            {
                itw.Write("args");
            }

            itw.WriteLine(");");
            itw.Write("return ");
            if (!mainMethod.ReturnsVoid)
            {
                itw.Write("exit_code");
            }
            else
            {
                itw.Write("0");
            }

            itw.WriteLine(";");

            itw.Indent--;
            itw.WriteLine("}");
        }

        public void WriteBuildFiles(AssemblyIdentity identity, ISet<AssemblyIdentity> references, bool executable)
        {
            // CMake file helper
            var cmake = @"cmake_minimum_required (VERSION 2.8.10 FATAL_ERROR)

file(GLOB_RECURSE <%name%>_SRC
    ""./src/*.cpp""
)

file(GLOB_RECURSE <%name%>_IMPL
    ""./impl/*.cpp""
)

include_directories(""./"" ""./src"" ""./impl"" <%include%>)

if (CMAKE_BUILD_TYPE STREQUAL ""Debug"")
    SET(BUILD_TYPE ""debug"")
else()
    SET(BUILD_TYPE ""release"")
endif()

if (MSVC)
    SET(BUILD_ARCH ""win32"")

    link_directories(""./"" <%links%>)
    SET(CMAKE_CXX_FLAGS_DEBUG ""${CMAKE_CXX_FLAGS_DEBUG} /Od /Zi /EHsc /DDEBUG /wd4250 /wd4200 /wd4291 /wd4996 /wd4800 /MP8"")
    SET(CMAKE_CXX_FLAGS_RELEASE ""${CMAKE_CXX_FLAGS_RELEASE} /Ox /EHsc /wd4250 /wd4200 /wd4291 /wd4996 /wd4800 /MP8"")
    set(CMAKE_EXE_LINKER_FLAGS ""${CMAKE_EXE_LINKER_FLAGS} /ignore:4006 /ignore:4049 /ignore:4217"")
else()
    if (CMAKE_SYSTEM_NAME STREQUAL ""Android"")
        SET(EXTRA_CXX_FLAGS ""-std=gnu++11 -fexceptions -frtti"")
        SET(BUILD_ARCH ""vs_android"")
    else()
        SET(EXTRA_CXX_FLAGS ""-std=gnu++14 -march=native"")
        SET(BUILD_ARCH ""mingw32"")
    endif()

    link_directories(""./"" <%links%>)
    SET(CMAKE_CXX_FLAGS_DEBUG ""${CMAKE_CXX_FLAGS_DEBUG} -O0 -ggdb -fvar-tracking-assignments -gdwarf-4 -DDEBUG ${EXTRA_CXX_FLAGS} -Wno-invalid-offsetof"")
    SET(CMAKE_CXX_FLAGS_RELEASE ""${CMAKE_CXX_FLAGS_RELEASE} -O2 ${EXTRA_CXX_FLAGS} -Wno-invalid-offsetof"")
endif()

add_<%type%> (<%name%> ""${<%name%>_SRC}"" ""${<%name%>_IMPL}"")

<%libraries%>";

            var targetLinkLibraries = @"
if (MSVC)
target_link_libraries (<%name%> {0} ""gcmt-lib"")
else()
target_link_libraries (<%name%> {0} ""stdc++"" ""gcmt-lib"")
endif()";

            var type = executable ? "executable" : "library";
            var include = string.Join(" ", references.Select(a => string.Format("\"../{0}/src\" \"../{0}/impl\"", a.Name.CleanUpNameAllUnderscore())));
            var links = string.Join(" ", references.Select(a => string.Format("\"../{0}/__build_{1}_{2}\" \"../{0}/__build_{1}_{2}_bdwgc\"", a.Name.CleanUpNameAllUnderscore(), "${BUILD_ARCH}", "${BUILD_TYPE}")));
            var libraries = string.Format(targetLinkLibraries, string.Join(" ", references.Select(a => string.Format("\"{0}\"", a.Name.CleanUpNameAllUnderscore()))));

            if (references.Any())
            {
                include += " \"../CoreLib/bdwgc/include\"";
            }
            else
            {
                include += " \"./bdwgc/include\"";
            }

            using (var itw = new IndentedTextWriter(new StreamWriter(this.GetPath("CMakeLists", ".txt"))))
            {
                itw.Write(
                    cmake.Replace("<%libraries%>", executable ? libraries : string.Empty)
                         .Replace("<%type%>", type)
                         .Replace("<%name%>", identity.Name.CleanUpNameAllUnderscore())
                         .Replace("<%include%>", include)
                         .Replace("<%links%>", links));
                itw.Close();
            }

            // build mingw32 DEBUG .bat
            var buildMinGw32 = @"md __build_mingw32_<%build_type_lowercase%>
cd __build_mingw32_<%build_type_lowercase%>
cmake -f .. -G ""MinGW Makefiles"" -DCMAKE_BUILD_TYPE=<%build_type%> -Wno-dev
mingw32-make -j 8 2>log";

            using (var itw = new IndentedTextWriter(new StreamWriter(this.GetPath("build_mingw32_debug", ".bat"))))
            {
                itw.Write(buildMinGw32.Replace("<%name%>", identity.Name.CleanUpNameAllUnderscore()).Replace("<%build_type%>", "Debug").Replace("<%build_type_lowercase%>", "debug"));
                itw.Close();
            }

            using (var itw = new IndentedTextWriter(new StreamWriter(this.GetPath("build_mingw32_release", ".bat"))))
            {
                itw.Write(buildMinGw32.Replace("<%name%>", identity.Name.CleanUpNameAllUnderscore()).Replace("<%build_type%>", "Release").Replace("<%build_type_lowercase%>", "release"));
                itw.Close();
            }

            // build Visual Studio .bat
            var buildVS2015 = @"md __build_win32_<%build_type_lowercase%>
cd __build_win32_<%build_type_lowercase%>
cmake -f .. -G ""Visual Studio 14"" -DCMAKE_BUILD_TYPE=<%build_type%> -Wno-dev
call ""%VS140COMNTOOLS%\..\..\VC\vcvarsall.bat"" amd64_x86
MSBuild ALL_BUILD.vcxproj /m:8 /p:Configuration=<%build_type%> /p:Platform=""Win32"" /toolsversion:14.0";

            using (var itw = new IndentedTextWriter(new StreamWriter(this.GetPath("build_vs2015_debug", ".bat"))))
            {
                itw.Write(buildVS2015.Replace("<%name%>", identity.Name.CleanUpNameAllUnderscore()).Replace("<%build_type%>", "Debug").Replace("<%build_type_lowercase%>", "debug"));
                itw.Close();
            }

            using (var itw = new IndentedTextWriter(new StreamWriter(this.GetPath("build_vs2015_release", ".bat"))))
            {
                itw.Write(buildVS2015.Replace("<%name%>", identity.Name.CleanUpNameAllUnderscore()).Replace("<%build_type%>", "Release").Replace("<%build_type_lowercase%>", "release"));
                itw.Close();
            }

            // build Visual Studio .bat
            var buildVS2015TegraAndroid = @"md __build_vs_android_<%build_type_lowercase%>
cd __build_vs_android_<%build_type_lowercase%>
cmake -f .. -G ""Visual Studio 14"" -DCMAKE_BUILD_TYPE=<%build_type%> -DCMAKE_SYSTEM_NAME=Android -Wno-dev
call ""%VS140COMNTOOLS%\..\..\VC\vcvarsall.bat"" amd64_x86
MSBuild ALL_BUILD.vcxproj /m:8 /p:Configuration=<%build_type%> /p:Platform=""Win32"" /toolsversion:14.0";

            using (var itw = new IndentedTextWriter(new StreamWriter(this.GetPath("build_vs_android_debug", ".bat"))))
            {
                itw.Write(buildVS2015TegraAndroid.Replace("<%name%>", identity.Name.CleanUpNameAllUnderscore()).Replace("<%build_type%>", "Debug").Replace("<%build_type_lowercase%>", "debug"));
                itw.Close();
            }

            using (var itw = new IndentedTextWriter(new StreamWriter(this.GetPath("build_vs_android_release", ".bat"))))
            {
                itw.Write(buildVS2015TegraAndroid.Replace("<%name%>", identity.Name.CleanUpNameAllUnderscore()).Replace("<%build_type%>", "Release").Replace("<%build_type_lowercase%>", "release"));
                itw.Close();
            }

            // prerequisite
            if (!references.Any())
            {
                var buildMinGw32Bdwgc = @"if not exist bdwgc (git clone git://github.com/ivmai/bdwgc.git bdwgc)
if not exist bdwgc/libatomic_ops (git clone git://github.com/ivmai/libatomic_ops.git bdwgc/libatomic_ops)
md __build_mingw32_<%build_type_lowercase%>_bdwgc 
cd __build_mingw32_<%build_type_lowercase%>_bdwgc
cmake -f ../bdwgc -G ""MinGW Makefiles"" -Denable_threads:BOOL=ON -Denable_parallel_mark:BOOL=ON -Denable_cplusplus:BOOL=ON -Denable_gcj_support:BOOL=ON -DCMAKE_BUILD_TYPE=<%build_type%> -DCMAKE_USE_WIN32_THREADS_INIT=ON -Wno-dev
mingw32-make -j 8 2>log";

                using (var itw = new IndentedTextWriter(new StreamWriter(this.GetPath("build_prerequisite_mingw32_debug", ".bat"))))
                {
                    itw.Write(buildMinGw32Bdwgc.Replace("<%name%>", identity.Name.CleanUpNameAllUnderscore()).Replace("<%build_type%>", "Debug").Replace("<%build_type_lowercase%>", "debug"));
                    itw.Close();
                }

                using (var itw = new IndentedTextWriter(new StreamWriter(this.GetPath("build_prerequisite_mingw32_release", ".bat"))))
                {
                    itw.Write(buildMinGw32Bdwgc.Replace("<%name%>", identity.Name.CleanUpNameAllUnderscore()).Replace("<%build_type%>", "Release").Replace("<%build_type_lowercase%>", "release"));
                    itw.Close();
                }

                // build Visual Studio .bat
                var buildVS2015Bdwgc = @"if not exist bdwgc (git clone git://github.com/ivmai/bdwgc.git bdwgc)
if not exist bdwgc/libatomic_ops (git clone git://github.com/ivmai/libatomic_ops.git bdwgc/libatomic_ops)
md __build_win32_<%build_type_lowercase%>_bdwgc
cd __build_win32_<%build_type_lowercase%>_bdwgc
cmake -f ../bdwgc -G ""Visual Studio 14"" -Denable_threads:BOOL=ON -Denable_parallel_mark:BOOL=ON -Denable_cplusplus:BOOL=ON -Denable_gcj_support:BOOL=ON -DCMAKE_BUILD_TYPE=<%build_type%> -Wno-dev
call ""%VS140COMNTOOLS%\..\..\VC\vcvarsall.bat"" amd64_x86
MSBuild ALL_BUILD.vcxproj /m:8 /p:Configuration=<%build_type%> /p:Platform=""Win32"" /toolsversion:14.0";

                using (var itw = new IndentedTextWriter(new StreamWriter(this.GetPath("build_prerequisite_vs2015_debug", ".bat"))))
                {
                    itw.Write(buildVS2015Bdwgc.Replace("<%name%>", identity.Name.CleanUpNameAllUnderscore()).Replace("<%build_type%>", "Debug").Replace("<%build_type_lowercase%>", "debug"));
                    itw.Close();
                }

                using (var itw = new IndentedTextWriter(new StreamWriter(this.GetPath("build_prerequisite_vs2015_release", ".bat"))))
                {
                    itw.Write(buildVS2015Bdwgc.Replace("<%name%>", identity.Name.CleanUpNameAllUnderscore()).Replace("<%build_type%>", "Release").Replace("<%build_type_lowercase%>", "release"));
                    itw.Close();
                }

                // build Visual Studio .bat
                var buildVS2015TegraAndroidBdwgc = @"if not exist bdwgc (git clone git://github.com/ivmai/bdwgc.git bdwgc)
if not exist bdwgc/libatomic_ops (git clone git://github.com/ivmai/libatomic_ops.git bdwgc/libatomic_ops)
md __build_vs_android_<%build_type_lowercase%>_bdwgc
cd __build_vs_android_<%build_type_lowercase%>_bdwgc
cmake -f ../bdwgc -G ""Visual Studio 14"" -Denable_threads:BOOL=ON -Denable_parallel_mark:BOOL=ON -Denable_cplusplus:BOOL=ON -Denable_gcj_support:BOOL=ON -DCMAKE_BUILD_TYPE=<%build_type%> -DCMAKE_SYSTEM_NAME=Android -Wno-dev
call ""%VS140COMNTOOLS%\..\..\VC\vcvarsall.bat"" amd64_x86
MSBuild ALL_BUILD.vcxproj /m:8 /p:Configuration=<%build_type%> /p:Platform=""Win32"" /toolsversion:14.0";

                using (var itw = new IndentedTextWriter(new StreamWriter(this.GetPath("build_prerequisite_vs_android_debug", ".bat"))))
                {
                    itw.Write(buildVS2015TegraAndroidBdwgc.Replace("<%name%>", identity.Name.CleanUpNameAllUnderscore()).Replace("<%build_type%>", "Debug").Replace("<%build_type_lowercase%>", "debug"));
                    itw.Close();
                }

                using (var itw = new IndentedTextWriter(new StreamWriter(this.GetPath("build_prerequisite_vs_android_release", ".bat"))))
                {
                    itw.Write(buildVS2015TegraAndroidBdwgc.Replace("<%name%>", identity.Name.CleanUpNameAllUnderscore()).Replace("<%build_type%>", "Release").Replace("<%build_type_lowercase%>", "release"));
                    itw.Close();
                } 
            }
        }

        public void WriteCoreLibSource(AssemblyIdentity identity, bool isCoreLib)
        {
            if (!isCoreLib)
            {
                return;
            }

            // write header
            var text = new StringBuilder();
            using (var itw = new IndentedTextWriter(new StringWriter(text)))
            {
                WriteSourceInclude(itw, identity);

                itw.WriteLine(Resources.c_definitions);
                itw.WriteLine(Resources.intrin);
                itw.WriteLine(Resources.decimals);

                itw.Close();
            }

            var newText = text.ToString();
            var path = this.GetPath(identity.Name, subFolder: "src", ext: ".cpp");

            if (IsNothingChanged(path, newText))
            {
                return;
            }

            using (var textFile = new StreamWriter(path))
            {
                textFile.Write(newText);
            }
        }

        public void WriteHeader(AssemblyIdentity identity, ISet<AssemblyIdentity> references, bool isCoreLib, IEnumerable<CCodeUnit> units, IEnumerable<string> includeHeaders)
        {
            // write header
            var text = new StringBuilder();
            using (var itw = new IndentedTextWriter(new StringWriter(text)))
            {
                itw.WriteLine("#ifndef HEADER_{0}", identity.Name.CleanUpName());
                itw.WriteLine("#define HEADER_{0}", identity.Name.CleanUpName());

                var c = new CCodeWriterText(itw);

                if (isCoreLib)
                {
                    itw.WriteLine(Resources.c_include);
                    itw.WriteLine(Resources.intrin_template);
                }
                else
                {
                    foreach (var reference in references)
                    {
                        itw.WriteLine("#include \"{0}.h\"", reference.Name);
                    }
                }

                // write forward declaration
                foreach (var unit in units)
                {
                    WriteForwardDeclarationForUnit(unit, itw, c);
                }

                itw.WriteLine();

                if (isCoreLib)
                {
                    itw.WriteLine(Resources.c_forward_declarations);
                }

                itw.WriteLine();

                // write full declaration
                foreach (var unit in units)
                {
                    WriteFullDeclarationForUnit(unit, itw, c);
                }

                if (isCoreLib)
                {
                    itw.WriteLine();
                    itw.WriteLine(Resources.c_declarations);
                    itw.WriteLine(Resources.c_template_definitions);
                    itw.WriteLine(Resources.overflow);
                }

                foreach (var unit in units)
                {
                    var namedTypeSymbol = (INamedTypeSymbol)unit.Type;
                    if (namedTypeSymbol.TypeKind == TypeKind.Delegate)
                    {
                        itw.WriteLine();
                        WriteNamespaceOpen(namedTypeSymbol, itw, c);
                        new CCodeDelegateWrapperClass(namedTypeSymbol).WriteTo(c);
                        WriteNamespaceClose(namedTypeSymbol, itw);
                    }
                }

                foreach (var includeHeader in includeHeaders)
                {
                    itw.WriteLine("#include \"{0}\"", includeHeader);
                }

                itw.WriteLine("#endif");

                itw.Close();
            }

            var path = this.GetPath(identity.Name, subFolder: "src");
            var newText = text.ToString();

            if (IsNothingChanged(path, newText))
            {
                return;
            }

            using (var textFile = new StreamWriter(path))
            {
                textFile.Write(newText);
            }
        }

        public void WriteSources(AssemblyIdentity identity, IEnumerable<CCodeUnit> units)
        {
            if (this.Concurrent)
            {
                // write all sources
                Parallel.ForEach(
                    units.Where(unit => !((INamedTypeSymbol)unit.Type).IsGenericType),
                    (unit) =>
                    {
                        this.WriteSource(identity, unit);
                        this.WriteSource(identity, unit, true);
                    });
            }
            else
            {
                // write all sources
                foreach (var unit in units.Where(unit => !((INamedTypeSymbol)unit.Type).IsGenericType))
                {
                    this.WriteSource(identity, unit);
                    this.WriteSource(identity, unit, true);
                }
            }
        }

        public IList<string> WriteTemplateSources(IEnumerable<CCodeUnit> units, bool stubs = false)
        {
            var headersToInclude = new List<string>();

            // write all sources
            foreach (var unit in units)
            {
                int nestedLevel;
                var root = !stubs ? "src" : "impl";

                if (stubs)
                {
                    var path = this.GetPath(unit, out nestedLevel, ".h", root, doNotCreateFolder: true);
                    if (File.Exists(path))
                    {
                        headersToInclude.Add(path.Substring(string.Concat(root, "\\").Length + this.currentFolder.Length + 1));

                        // do not overwrite an existing file
                        continue;
                    }
                }

                var anyRecord = false;
                var text = new StringBuilder();
                using (var itw = new IndentedTextWriter(new StringWriter(text)))
                {
                    var c = new CCodeWriterText(itw);
                    itw.WriteLine("#ifndef HEADER_{0}", unit.Type.GetTypeFullName().CleanUpName());
                    itw.WriteLine("#define HEADER_{0}", unit.Type.GetTypeFullName().CleanUpName());

                    WriteNamespaceOpen((INamedTypeSymbol)unit.Type, itw, c);

                    foreach (var definition in unit.Definitions.Where(d => d.IsGeneric && d.IsStub == stubs))
                    {
                        anyRecord = true;
                        definition.WriteTo(c);
                    }

                    if (!stubs)
                    {
                        var namedTypeSymbol = (INamedTypeSymbol)unit.Type;
                        // write interface wrappers
                        foreach (var iface in unit.Type.Interfaces)
                        {
                            anyRecord |= WriteInterfaceWrapperImplementation(c, iface, namedTypeSymbol, true);
                        }
                    }

                    WriteNamespaceClose((INamedTypeSymbol)unit.Type, itw);

                    itw.WriteLine("#endif");

                    itw.Close();
                }

                if (anyRecord && text.Length > 0)
                {
                    var path = this.GetPath(unit, out nestedLevel, ".h", root);
                    var newText = text.ToString();

                    headersToInclude.Add(path.Substring(string.Concat(root, "\\").Length + this.currentFolder.Length + 1));

                    if (IsNothingChanged(path, newText))
                    {
                        continue;
                    }

                    using (var textFile = new StreamWriter(path))
                    {
                        textFile.Write(newText);
                    }
                }
            }

            return headersToInclude;
        }

        public void WriteTo(AssemblyIdentity identity, ISet<AssemblyIdentity> references, bool isCoreLib, bool isLibrary, IEnumerable<CCodeUnit> units, string outputFolder, string[] impl)
        {
            this.currentFolder = Path.Combine(outputFolder, identity.Name);
            if (!Directory.Exists(this.currentFolder))
            {
                Directory.CreateDirectory(this.currentFolder);
            }

            if (isCoreLib)
            {
                this.ExtractCoreLibImpl();
            }

            if (impl != null && impl.Any())
            {
                this.PopulateImpl(impl);
            }

            var includeHeaders = this.WriteTemplateSources(units).Union(this.WriteTemplateSources(units, true));

            this.WriteHeader(identity, references, isCoreLib, units, includeHeaders);

            this.WriteCoreLibSource(identity, isCoreLib);

            this.WriteSources(identity, units);

            this.WriteBuildFiles(identity, references, !isLibrary);
        }

        private void ExtractCoreLibImpl()
        {
            var implFolder = Path.Combine(this.currentFolder, "Impl");
            // extract Impl file
            using (var archive = new ZipArchive(new MemoryStream(Resources.Impl)))
            {
                foreach (var file in archive.Entries)
                {
                    var completeFileName = Path.Combine(implFolder, file.FullName);
                    var directoryName = Path.GetDirectoryName(completeFileName);
                    if (!Directory.Exists(directoryName))
                    {
                        Directory.CreateDirectory(directoryName);
                        file.ExtractToFile(completeFileName);
                    }
                    else if (!File.Exists(completeFileName))
                    {
                        file.ExtractToFile(completeFileName);
                    }
                }
            }
        }

        private void PopulateImpl(string[] impl)
        {
            var delimeterFolder = "\\Impl\\";
            foreach (var file in impl)
            {
                var completeFileName = string.Concat(this.currentFolder, file.Substring(file.IndexOf(delimeterFolder, StringComparison.OrdinalIgnoreCase)));
                var directoryName = Path.GetDirectoryName(completeFileName);
                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                File.Copy(file, completeFileName, true);
            }
        }

        private static string GetRelativePath(CCodeUnit unit, out int nestedLevel)
        {
            var enumNamespaces = unit.Type.ContainingNamespace.EnumNamespaces().Where(n => !n.IsGlobalNamespace).ToList();
            nestedLevel = enumNamespaces.Count();
            return String.Join("\\", enumNamespaces.Select(n => n.MetadataName.ToString().CleanUpNameAllUnderscore()));
        }

        private static bool IsNothingChanged(string path, string newText)
        {
            // check if file exist and overwrite only if different in size of HashCode
            if (!File.Exists(path))
            {
                return false;
            }

            var fileInfo = new FileInfo(path);
            if (fileInfo.Length != newText.Length)
            {
                return false;
            }

            // sizes the same, check HashValues
            using (var hashAlorithm = new SHA1CryptoServiceProvider())
            using (var textFile = new StreamReader(path))
            {
                var newHash = hashAlorithm.ComputeHash(Encoding.UTF8.GetBytes(newText));
                var originalHash = hashAlorithm.ComputeHash(textFile.BaseStream);
                var isNothingChanged = StructuralComparisons.StructuralEqualityComparer.Equals(newHash, originalHash);
                return isNothingChanged;
            }
        }

        private static void WriteEnum(IndentedTextWriter itw, CCodeWriterText c, INamedTypeSymbol namedTypeSymbol)
        {
            itw.WriteLine();
            itw.Write("enum class ");
            c.WriteTypeName(namedTypeSymbol, false, true);
            itw.Write(" : ");
            c.WriteType(namedTypeSymbol.EnumUnderlyingType);

            c.NewLine();
            c.OpenBlock();

            var constantValueTypeDiscriminator = namedTypeSymbol.EnumUnderlyingType.SpecialType.GetDiscriminator();

            var any = false;
            foreach (var constValue in namedTypeSymbol.GetMembers().OfType<IFieldSymbol>().Where(f => f.IsConst))
            {
                if (any)
                {
                    c.TextSpan(",");
                    c.WhiteSpace();
                }

                c.TextSpan("c_");
                c.WriteName(constValue);
                if (constValue.ConstantValue != null)
                {
                    c.TextSpan(" = ");
                    new Literal { Value = ConstantValue.Create(constValue.ConstantValue, constantValueTypeDiscriminator) }
                        .WriteTo(c);
                }

                any = true;
            }

            c.EndBlockWithoutNewLine();
            c.EndStatement();
        }

        private static void WriteForwardDeclarationForUnit(CCodeUnit unit, IndentedTextWriter itw, CCodeWriterText c)
        {
            var namedTypeSymbol = (INamedTypeSymbol)unit.Type;
            foreach (var namespaceNode in namedTypeSymbol.ContainingNamespace.EnumNamespaces())
            {
                itw.Write("namespace ");
                c.WriteNamespaceName(namespaceNode);
                itw.Write(" { ");
            }

            if (namedTypeSymbol.IsGenericType)
            {
                c.WriteTemplateDeclaration(namedTypeSymbol);
            }

            itw.Write(namedTypeSymbol.IsValueType ? "struct" : "class");
            itw.Write(" ");
            c.WriteTypeName(namedTypeSymbol, false);
            itw.Write("; ");

            if (namedTypeSymbol.TypeKind == TypeKind.Enum)
            {
                WriteEnum(itw, c, namedTypeSymbol);
            }

            foreach (var namespaceNode in namedTypeSymbol.ContainingNamespace.EnumNamespaces())
            {
                itw.Write("}");
            }

            itw.WriteLine();

            if (namedTypeSymbol.SpecialType == SpecialType.System_Object || namedTypeSymbol.SpecialType == SpecialType.System_String)
            {
                itw.Write("typedef ");
                c.WriteType(namedTypeSymbol, suppressReference: true, allowKeywords: false);
                itw.Write(" ");
                c.WriteTypeName(namedTypeSymbol);
                itw.WriteLine(";");
            }
        }

        private static void WriteFullDeclarationForUnit(CCodeUnit unit, IndentedTextWriter itw, CCodeWriterText c)
        {
            var namedTypeSymbol = (INamedTypeSymbol)unit.Type;
            WriteNamespaceOpen(namedTypeSymbol, itw, c);

            // write extern declaration
            var externDeclarations = unit.Declarations.Select(
                declaration => new { declaration, codeMethodDeclaration = declaration as CCodeMethodDeclaration })
                .Where(@t => @t.codeMethodDeclaration != null && @t.codeMethodDeclaration.IsExternDeclaration)
                .Select(@t => @t.declaration).ToList();
            if (externDeclarations.Any())
            {
                itw.Write("extern \"C\"");
                c.WhiteSpace();
                c.OpenBlock();

                foreach (var declaration in externDeclarations)
                {
                    declaration.WriteTo(c);
                }

                c.EndBlock();
            }

            if (namedTypeSymbol.IsGenericType)
            {
                c.WriteTemplateDeclaration(namedTypeSymbol);
            }

            itw.Write(namedTypeSymbol.IsValueType ? "struct" : "class");
            itw.Write(" ");
            c.WriteTypeName(namedTypeSymbol, false);
            if (namedTypeSymbol.BaseType != null)
            {
                itw.Write(" : public ");
                c.WriteTypeFullName(namedTypeSymbol.BaseType);
            }

            itw.WriteLine();
            itw.WriteLine("{");
            itw.WriteLine("public:");
            itw.Indent++;

            // base declaration
            if (namedTypeSymbol.BaseType != null)
            {
                itw.Write("typedef ");
                c.WriteTypeFullName(namedTypeSymbol.BaseType, false);
                itw.WriteLine(" base;");
            }

            foreach (var method in namedTypeSymbol.IterateAllMethodsWithTheSameNamesTakeOnlyOne())
            {
                c.TextSpan("using");
                c.WhiteSpace();
                c.WriteType(namedTypeSymbol.BaseType ?? method.ReceiverType, suppressReference: true, allowKeywords: true);
                c.TextSpan("::");
                c.WriteMethodName(method);
                c.TextSpan(";");
                c.NewLine();
            }

            if (namedTypeSymbol.TypeKind == TypeKind.Enum)
            {
                // value holder for enum
                c.WriteType(namedTypeSymbol);
                itw.WriteLine(" m_value;");
            }

            if (namedTypeSymbol.IsRuntimeType())
            {
                c.WriteTypeName(namedTypeSymbol, false);
                itw.WriteLine("() = default;");
            }

            /*
            if (namedTypeSymbol.IsIntPtrType())
            {
                c.WriteTypeName(namedTypeSymbol, false);
                itw.WriteLine("() = default;");
            }
            */

            foreach (var declaration in unit.Declarations)
            {
                var codeMethodDeclaration = declaration as CCodeMethodDeclaration;
                if (codeMethodDeclaration == null || !codeMethodDeclaration.IsExternDeclaration)
                {
                    declaration.WriteTo(c);
                }
            }

            // write interface wrappers
            foreach (var iface in namedTypeSymbol.Interfaces)
            {
                WriteInterfaceWrapper(c, iface, namedTypeSymbol);
            }

            itw.Indent--;
            itw.WriteLine("};");

            WriteNamespaceClose(namedTypeSymbol, itw);

            if (namedTypeSymbol.IsPrimitiveValueType() || namedTypeSymbol.TypeKind == TypeKind.Enum || namedTypeSymbol.SpecialType == SpecialType.System_Void)
            {
                // value to class
                c.TextSpanNewLine("template<>");
                c.TextSpan("struct");
                c.WhiteSpace();
                c.TextSpan("valuetype_to_class<");
                c.WriteType(namedTypeSymbol);
                c.TextSpan(">");
                c.WhiteSpace();
                c.TextSpan("{ typedef");
                c.WhiteSpace();
                c.WriteType(namedTypeSymbol, true, false, true);
                c.WhiteSpace();
                c.TextSpanNewLine("type; };");

                // class to value
                c.TextSpanNewLine("template<>");
                c.TextSpan("struct");
                c.WhiteSpace();
                c.TextSpan("class_to_valuetype<");
                c.WriteType(namedTypeSymbol, true, false, true);
                c.TextSpan(">");
                c.WhiteSpace();
                c.TextSpan("{ typedef");
                c.WhiteSpace();
                c.WriteType(namedTypeSymbol);
                c.WhiteSpace();
                c.TextSpanNewLine("type; };");

                // map class to valuetype
                if (namedTypeSymbol.IsAtomicType())
                {
                    c.TextSpanNewLine("template<>");
                    c.TextSpanNewLine("struct gc_traits<");
                    c.WriteType(namedTypeSymbol, true, false, true);
                    c.TextSpanNewLine("> { constexpr static const GCAtomic value = GCAtomic::Default; };");
                }
            }
        }

        private static void WriteNamespaceOpen(INamedTypeSymbol namedTypeSymbol, IndentedTextWriter itw, CCodeWriterText c)
        {
            bool any = false;
            foreach (var namespaceNode in namedTypeSymbol.ContainingNamespace.EnumNamespaces())
            {
                itw.Write("namespace ");
                c.WriteNamespaceName(namespaceNode);
                itw.Write(" { ");
                any = true;
            }

            if (any)
            {
                itw.Indent++;
                itw.WriteLine();
            }
        }

        private static void WriteNamespaceClose(INamedTypeSymbol namedTypeSymbol, IndentedTextWriter itw)
        {
            foreach (var namespaceNode in namedTypeSymbol.ContainingNamespace.EnumNamespaces())
            {
                itw.Indent--;
                itw.Write("}");
            }

            itw.WriteLine();
        }

        private static void WriteInterfaceWrapper(CCodeWriterText c, INamedTypeSymbol iface, INamedTypeSymbol namedTypeSymbol)
        {
            new CCodeInterfaceWrapperClass(namedTypeSymbol, iface).WriteTo(c);
            c.EndStatement();
            new CCodeInterfaceCastOperatorDeclaration(namedTypeSymbol, iface).WriteTo(c);
        }

        private static bool WriteInterfaceWrapperImplementation(CCodeWriterText c, INamedTypeSymbol iface, INamedTypeSymbol namedTypeSymbol, bool genericHeaderFile = false)
        {
            var anyRecord = false;

            foreach (var interfaceMethodWrapper in new CCodeInterfaceWrapperClass(namedTypeSymbol, iface).GetMembersImplementation())
            {
                var allowedMethod = !genericHeaderFile || (namedTypeSymbol.IsGenericType || interfaceMethodWrapper.IsGeneric);
                if (!allowedMethod)
                {
                    continue;
                }

                interfaceMethodWrapper.WriteTo(c);
                anyRecord = true;
            }

            return anyRecord;
        }

        private string GetPath(string name, string ext = ".h", string subFolder = "")
        {
            var fullDirPath = Path.Combine(this.currentFolder, subFolder);
            var fullPath = Path.Combine(fullDirPath, String.Concat(name, ext));
            if (!Directory.Exists(fullDirPath))
            {
                Directory.CreateDirectory(fullDirPath);
            }

            return fullPath;
        }

        private string GetPath(CCodeUnit unit, out int nestedLevel, string ext = ".cpp", string folder = "src", bool doNotCreateFolder = false)
        {
            var fileRelativePath = GetRelativePath(unit, out nestedLevel);
            var fullDirPath = Path.Combine(this.currentFolder, folder, fileRelativePath);
            if (!doNotCreateFolder && !Directory.Exists(fullDirPath))
            {
                Directory.CreateDirectory(fullDirPath);
            }

            var fullPath = Path.Combine(fullDirPath, string.Concat(unit.Type.GetTypeName().CleanUpNameAllUnderscore(), ext));
            return fullPath;
        }

        private void WriteSource(AssemblyIdentity identity, CCodeUnit unit, bool stubs = false)
        {
            int nestedLevel;

            if (stubs)
            {
                var path = this.GetPath(unit, out nestedLevel, folder: !stubs ? "src" : "impl", doNotCreateFolder: true);
                if (File.Exists(path))
                {
                    // do not overwrite an existing file
                    return;
                }
            }

            var anyRecord = false;
            var text = new StringBuilder();
            using (var itw = new IndentedTextWriter(new StringWriter(text)))
            {
                var c = new CCodeWriterText(itw);

                WriteSourceInclude(itw, identity);

                var namedTypeSymbol = (INamedTypeSymbol)unit.Type;
                WriteNamespaceOpen(namedTypeSymbol, itw, c);

                foreach (var definition in unit.Definitions.Where(d => !d.IsGeneric && d.IsStub == stubs))
                {
                    anyRecord = true;
                    definition.WriteTo(c);
                }

                if (!stubs)
                {
                    // write interface wrappers
                    foreach (var iface in unit.Type.Interfaces)
                    {
                        anyRecord |= WriteInterfaceWrapperImplementation(c, iface, namedTypeSymbol);
                    }
                }

                WriteNamespaceClose(namedTypeSymbol, itw);

                if (!stubs && unit.MainMethod != null)
                {
                    WriteSourceMainEntry(c, itw, unit.MainMethod);
                }

                itw.Close();
            }

            if (anyRecord && text.Length > 0)
            {
                var path = this.GetPath(unit, out nestedLevel, folder: !stubs ? "src" : "impl");
                var newText = text.ToString();

                if (IsNothingChanged(path, newText))
                {
                    return;
                }

                using (var textFile = new StreamWriter(path))
                {
                    textFile.Write(newText);
                }
            }
        }
    }
}
