﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MSTests.cs" company="">
//   
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Ll2NativeTests
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    using Il2Native.Logic;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Summary description for MSTests
    /// </summary>
    [TestClass]
    public class MSTests
    {
#if _DISK_C_

        /// <summary>
        /// </summary>
        private const string SourcePath = @"C:\Temp\CSharpTranspilerExt\Mono-Class-Libraries\mcs\tests\";

        /// <summary>
        /// </summary>
        private const string SourcePathCustom = @"C:\Temp\tests\";

        /// <summary>
        /// </summary>
        private const string OutputPath = @"C:\Temp\IlCTests\";

        /// <summary>
        /// </summary>
        private const string CoreLibPath = @"C:\Dev\Temp\Il2Native\CoreLib\bin\Release\CoreLib.dll";

        /// <summary>
        /// </summary>
        private const string OpenGlLibPath = @"C:\Dev\BabylonNative\BabylonNativeCs\BabylonNativeCsLibraryForIl\bin\Release\BabylonNativeCsLibraryForIl.dll";

        /// <summary>
        /// </summary>
        private const string OpenGlExePath = @"C:\Dev\BabylonNative\BabylonNativeCs\BabylonGlut\bin\Release\BabylonGlut.dll";
#endif
#if _DISK_D_
        private const string SourcePath = @"D:\Temp\CSharpTranspilerExt\Mono-Class-Libraries\mcs\tests\";
        private const string SourcePathCustom = @"D:\Temp\tests\";
        private const string OutputPath = @"D:\Temp\IlCTests\";
        private const string CoreLibPath = @"..\..\..\CoreLib\bin\Release\CoreLib.dll";
        private const string OpenGlLibPath = @"D:\Developing\BabylonNative\BabylonNativeCs\BabylonNativeCsLibraryForIl\bin\Debug\BabylonNativeCsLibraryForIl.dll";
        private const string OpenGlExePath = @"D:\Developing\BabylonNative\BabylonNativeCs\BabylonGlut\bin\Debug\BabylonGlut.dll";
#endif

        /// <summary>
        /// </summary>
        private const bool UsingRoslyn = true;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        #region Additional test attributes

        // You can use the following additional attributes as you write your tests:
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        #endregion

        /// <summary>
        /// </summary>
        [TestMethod]
        public void TestCustomConvert()
        {
            Convert(1, SourcePathCustom);
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void TestCoreLib()
        {
            Il2Converter.Convert(Path.GetFullPath(CoreLibPath), OutputPath, GetConverterArgs(false));
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void TestOpenGlLib()
        {
            // todo: 
            // 1) class Condition method _getEffectiveTarget when it is returning Object it does not cast Interface to an Object, to replicate the issue change returning type to Object
            // 2) the same as 1) but in InterpolateValueAction when saving value 'value = this._target[this._property]'
            Il2Converter.Convert(Path.GetFullPath(OpenGlLibPath), OutputPath, GetConverterArgs(true));
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void TestOpenGlExe()
        {
            Il2Converter.Convert(Path.GetFullPath(OpenGlExePath), OutputPath, GetConverterArgs(true));
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void TestCompile()
        {
            var skip = new List<int>(new int[] { 10, 19, 39, 50, 67 });

            if (UsingRoslyn)
            {
                // 49 - bug in execution
                skip.AddRange(new int[] { 83 });
            }

            foreach (var index in Enumerable.Range(1, 729).Where(n => !skip.Contains(n)))
            {
                Compile(index);
            }
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void TestCompileAndRunLlvm()
        {
            // 1) !!! NEED TO BE FIXED, Issue: dynamic_cast of a Struct

            // 10 - not compilable
            // 19 - using Thread class
            // 28 - bug in execution (Hashtable)
            // 32 - multi array
            // 33 - using GetType
            // 36 - bug in execution (NotImplemented)
            // 37 - multi array
            // 39 - using Attributes
            // 43 - multi array
            // 45 - NEED TO BE FIXED: arrays initialization
            // 50 - missing
            // 52 - bug in execution (NotImplemented, ArrayList, Hashtable)
            // 57 - bug in execution (NotImplemented, EventHandler)
            // 67 - missing
            // 68 - using enum
            // 74 - using StreamReader
            // 77 - using enum
            // 83 - using System.Threading.Interlocked.CompareExchange
            // 85 - using UnmanagedType
            // 91 - using Reflection
            // 95 - NEED TO BE FIXED, init double in a class
            // 99 - using GetType
            // 100 - using DllImport      
            // 101 - using Reflection
            // 102 - using Reflection
            // 104 - System.Threading.Interlocked.Increment
            // 105 - IAsyncResult (NotImplemented)
            // 106 - IAsyncResult (NotImplemented) (missing)
            // 109 - DateTime.Now.ToString (NotImplemented)
            // 115 - explicit cast (cast class without explicit operator should throw an exception)
            // 117 - not implemented Hashtable
            // 118 - not implemented Attribute
            // 120 - not implemented Attribute
            // 126 - not implemented defualt ToString() to return type name
            // 127 - using typeof
            // 128 - using typeof
            // 129 - using typeof
            // 130 - not compilable (Debug Trace: (24,20): error CS0037: Cannot convert null to 'System.IntPtr' because it is a non-nullable value type)
            // 132 - typeof, Reflection
            // 135 - typeof, Reflection
            var skip =
                new List<int>(
                    new[]
                        {
                            10, 19, 28, 33, 36, 39, 45, 50, 52, 53, 57, 67, 68, 83, 85, 91, 95, 99, 100, 101, 102, 104, 105, 106, 107, 109, 115, 117, 118, 120,
                            126, 127, 128, 129, 130, 132, 135
                        });

            if (UsingRoslyn)
            {
                // 49 - bug in execution
                skip.AddRange(new[] { 49 });
            }

            foreach (var index in Enumerable.Range(1, 729).Where(n => !skip.Contains(n)))
            {
                CompileAndRun(index);
            }
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void TestGenCompileAndRunLlvm()
        {
            // 21 - using default on Class causing Boxing of Reference type
            // 29 - boxing array and sends to WriteLine - causes crash
            // 40 - using T name in nested generic type which causes mess (not main concern now), Debug Trace: (46,19): warning CS0693: Type parameter 'T' has the same name as the type parameter from outer type 'Stack<T>'
            // 46 - using Event, Debug Trace: (9,23): error CS0656: Missing compiler required member 'System.Threading.Interlocked.CompareExchange'
            // 47 - not compilable
            // 51 - bug in execution (NotImplemented)
            // 52 - using new() (NEED TO BE FIXED), Debug Trace: (9,10): error CS0656: Missing compiler required member 'System.Activator.CreateInstance'
            // 56 - bug in execution (NotImplemented)
            // 57 - generic virtual methods in an interface
            var skip = new[] { 21, 29, 40, 46, 47, 51, 52, 56, 57 };
            foreach (var index in Enumerable.Range(1, 400).Where(n => !skip.Contains(n)))
            {
                GenCompileAndRun(index);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="includeCoreLib">
        /// </param>
        /// <param name="roslyn">
        /// </param>
        /// <returns>
        /// </returns>
        private static string[] GetConverterArgs(bool includeCoreLib, bool roslyn = UsingRoslyn)
        {
            var args = new List<string>();
            if (includeCoreLib)
            {
                args.Add("corelib:" + Path.GetFullPath(CoreLibPath));
            }

            if (roslyn)
            {
                args.Add("roslyn");
            }

            return args.ToArray();
        }

        /// <summary>
        /// </summary>
        /// <param name="index">
        /// </param>
        /// <param name="fileName">
        /// </param>
        /// <param name="format">
        /// </param>
        /// <param name="justCompile">
        /// </param>
        private static void ExecCompile(int index, string fileName = "test", string format = null, bool justCompile = false)
        {
            /*
                call vcvars32.bat
                llc -mtriple i686-pc-win32 -filetype=obj corelib.ll
                llc -mtriple i686-pc-win32 -filetype=obj test-%1.ll
                link -defaultlib:libcmt -nodefaultlib:msvcrt.lib -nodefaultlib:libcd.lib -nodefaultlib:libcmtd.lib -nodefaultlib:msvcrtd.lib corelib.obj test-%1.obj /OUT:test-%1.exe
                del test-%1.obj
            */

            // to test working try/catch with g++ compilation
            // http://mingw-w64.sourceforge.net/download.php
            // Windows 32	DWARF	i686 - use this config to test exceptions on windows
            /*
                llc -mtriple i686-pc-mingw32 -filetype=obj corelib.ll
                llc -mtriple i686-pc-mingw32 -filetype=obj test-%1.ll
                g++.exe -o test-%1.exe corelib.o test-%1.o -lstdc++ -march=i686
                del test-%1.o
            */
            var pi = new ProcessStartInfo();
            pi.WorkingDirectory = OutputPath;
            pi.FileName = "ll.bat";
            pi.Arguments = string.Format("{1}-{0}", format == null ? index.ToString() : index.ToString(format), fileName);

            var process = Process.Start(pi);

            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode);

            if (!justCompile)
            {
                var execProcess = new ProcessStartInfo();
                execProcess.WorkingDirectory = OutputPath;
                execProcess.FileName = string.Format("{1}{2}-{0}.exe", format == null ? index.ToString() : index.ToString(format), OutputPath, fileName);

                var execProcessProc = Process.Start(execProcess);

                execProcessProc.WaitForExit();

                Assert.AreEqual(0, execProcessProc.ExitCode);
            }
            else
            {
                Assert.IsTrue(File.Exists(Path.Combine(OutputPath, string.Format("{1}{2}-{0}.exe", format == null ? index.ToString() : index.ToString(format), OutputPath, fileName))));
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="index">
        /// </param>
        private static void Compile(int index)
        {
            Trace.WriteLine("Generating LLVM BC(ll) for " + index);

            Convert(index);

            Trace.WriteLine("Compiling LLVM for " + index);

            ExecCompile(index, "test", null, true);
        }

        /// <summary>
        /// </summary>
        /// <param name="index">
        /// </param>
        private static void CompileAndRun(int index)
        {
            Trace.WriteLine("Generating LLVM BC(ll) for " + index);

            Convert(index);

            Trace.WriteLine("Compiling/Executing LLVM for " + index);

            ExecCompile(index);
        }

        /// <summary>
        /// </summary>
        /// <param name="number">
        /// </param>
        /// <param name="source">
        /// </param>
        /// <param name="fileName">
        /// </param>
        /// <param name="format">
        /// </param>
        private static void Convert(int number, string source = SourcePath, string fileName = "test", string format = null)
        {
            Il2Converter.Convert(
                string.Concat(source, string.Format("{1}-{0}.cs", format == null ? number.ToString() : number.ToString(format), fileName)),
                OutputPath,
                GetConverterArgs(true));
        }

        /// <summary>
        /// </summary>
        /// <param name="index">
        /// </param>
        private static void GenCompileAndRun(int index)
        {
            Trace.WriteLine("Generating LLVM BC(ll) for " + index);

            Convert(index, SourcePath, "gtest", "000");

            Trace.WriteLine("Executing LLVM for " + index);

            ExecCompile(index, "gtest", "000");
        }
    }
}