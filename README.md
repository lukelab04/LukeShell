# LukeShell
A custom Linux shell with built-in support for LukeScript

# Installation

Download this repository. You will need the .NET and C# tools for Linux (Instructions can be found [here](https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu#2104-)).

Run ```dotnet new console``` to create a C# project. You can delete the Project.cs file that it creates.

Run ```dotnet build``` to build the project. You may need to install some dependancies with Nuget.

Run ```make run``` to compile main.cpp.

Run ```./main``` to run the program.

# Commands
All of the basic Linux commands should work. However, piping and file I/O is not yet supported. 

A few special commands:

To clear the terminal, enter ```clear```

To exit, enter ```exit```

To use LukeScript, enter ```lukescript``` followed by either your program or ```-f``` plus a path to a text file conataining your program. 

For example, ```lukescript print("Hello");``` will print Hello to the console. ```lukescript -f myProgram.txt``` will run whatever LukeScript program is contained in myProgram.txt.

For a more complete tutorial on using LukeScript, go [here](https://github.com/lukelab04/LukeScript).
