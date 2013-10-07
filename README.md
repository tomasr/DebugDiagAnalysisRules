# DebugDiag Sample Analysis Rules

This is a set of sample dump analysis rules for [http://blogs.msdn.com/b/debugdiag/archive/2013/10/03/debugdiag-2-0-is-now-rtw.aspx](DebugDiag v2.0):

* SPListAnalysis: performance analysis for SPList queries.


## Usage Instructions

1. Download and install DebugDiag v2.0 from http://www.microsoft.com/en-us/download/details.aspx?id=40336
2. Open Winterdom.DebugDiag.AnalysisRules.csproj in a text editor.
3. Locate the <DebugDiagLocation> element and modify it to point to your DebugDiag installation. VS will pick up the reference assemblies from this location, and will try to copy the resulting assembly to the <DebugDiag>\AnalysisRules folder.
```xml
        <PropertyGroup>
                <DebugDiagLocation>c:\DebugDiag2.0\</DebugDiagLocation>
        </PropertyGroup>
```

4. Open the project in Visual Studio, build it, and then test it.

That's all!
