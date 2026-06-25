using System.Runtime.CompilerServices;
using System.Windows;

// Erlaubt dem Testprojekt den Zugriff auf interne Typen/Member (z. B. ThinkFilter,
// OllamaService.BuildRequest), ohne sie öffentlich machen zu müssen.
[assembly: InternalsVisibleTo("Clap.Tests")]

[assembly:ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]
