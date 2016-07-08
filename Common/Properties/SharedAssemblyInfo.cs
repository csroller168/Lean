﻿using System.Reflection;
using System.Runtime.CompilerServices;

// common assembly attributes
[assembly: AssemblyDescription("Lean Algorithmic Trading Engine - QuantConnect.com")]
[assembly: AssemblyCopyright("Copyright ©  2015")]
[assembly: AssemblyCompany("QuantConnect")]
[assembly: AssemblyTrademark("QuantConnect")]
[assembly: AssemblyVersion("2.2.0.2")]

[assembly: InternalsVisibleTo("QuantConnect.Tests")]

#if DEBUG
    [assembly: AssemblyConfiguration("Debug")]
#else
    [assembly: AssemblyConfiguration("Release")]
#endif