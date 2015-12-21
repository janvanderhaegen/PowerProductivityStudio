# PowerProductivityStudio
PPS is a MSBuild task for Visual Studio LightSwitch projects that adds generic event hooks.
*Warning*: dragons be here! The source code was never properly cleaned up, documented, and contains a lot of mindfcuk code. Proceed at your own risk.

Since the amount of LightSwitch time I spend professionally went from 100% to near-zero, I can no longer maintain this project for the ever shrinking community on my own.
Please do a pull request if you made any changes that might help other developers, or ask me to become a contributor! 

## Run the project
Select MSBuild as the startup project.
Since this is an MSBuild task, your debugger should debug another Visual Studio instance.
In the properties of the MSBuild project, set "Run External Program" to launch your Visual Studio. You might want to pass a solution as a command line argument.

## Build nuget package
Bump the version number.
Right click on PackageBuild > Debug > Start new instance.


Keep rocking LS!


J
