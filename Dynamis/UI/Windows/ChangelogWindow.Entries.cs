namespace Dynamis.UI.Windows;

partial class ChangelogWindow
{
    private void Draw0_1_4_0()
    {
        if (!DrawVersionHeader(0, 1, 4, 0, 1)) {
            return;
        }

        ImGuiComponents.SeparatorText("New features");

        BulletText("Added an item in Dalamud's dev menu.");
        BulletText("Improved the pointer/address inputs (ImGui fields and /command arguments) to accept foo.exe+0x1234 and sub_140001234 syntaxes.");
        BulletText("Added Direct3D and DXGI interface identification, and object description display where applicable.");
        BulletText("Added manual class specification to Object Inspector.");
        BulletText("Added a changelog. (You are currently reading it!)");

        ImGuiComponents.SeparatorText("Inter-Plugin API");

        BulletText("Added InspectObject v3, ImGuiDrawPointer v4 and equivalent Get...Delegate IPC functions.");
        BulletText("The API version is now 1.7.");

        ImGuiComponents.SeparatorText("Bug fixes and improvements");

        BulletText("Changed the UI colors to use Dalamud's new semantic colors.");
        BulletText("Sorted well-known objects by name in the Object Inspector's menu.");
        BulletText("Added a button to copy Resource Handle file names to clipboard.");
        BulletText("Added a right-click menu to ImGui pointer fields to copy the pointers to clipboard in various forms.");
        BulletText("Added the ability to specify a class manually in the Get-ClientStruct cmdlet.");
        BulletText("Exposed the RSV/RSF viewer in the menus.");
        BulletText("Truncated functions larger than 4 kiB in the disassembler to avoid performance issues.");
        BulletText("Improved string display in the annotated mode hex viewers.");
        BulletText("Improved handling of objects of unknown size.");
        BulletText("Improved support for pointer fields of type known to ClientStructs.");
    }

    private void Draw0_1_3_14()
    {
        if (!DrawVersionHeader(0, 1, 3, 14, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("New features");

        BulletText("Added a RSV/RSF viewer, accessible through /dynamis rsv.");
    }

    private void Draw0_1_3_13()
    {
        if (!DrawVersionHeader(0, 1, 3, 13, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("New features");

        BulletText("Added a button to save Texture objects to TEX or DDS files.");
    }

    private void Draw0_1_3_12()
    {
        if (!DrawVersionHeader(0, 1, 3, 12, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("Miscellaneous");

        BulletText("Updated for Dalamud 15.");
    }

    private void Draw0_1_3_11()
    {
        if (!DrawVersionHeader(0, 1, 3, 11, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("Miscellaneous");

        BulletText("Updated for Dalamud 14.");
        BulletText("Updated to .NET 10.");
    }

    private void Draw0_1_3_10()
    {
        if (!DrawVersionHeader(0, 1, 3, 10, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("Bug fixes and improvements");

        BulletText("Made data.yml parsing more robust against duplicates.");
    }

    private void Draw0_1_3_9()
    {
        if (!DrawVersionHeader(0, 1, 3, 9, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("Bug fixes and improvements");

        BulletText("Fixed an out-of-bounds error in the hex viewers in annotated mode.");
    }

    private void Draw0_1_3_8()
    {
        if (!DrawVersionHeader(0, 1, 3, 8, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("Bug fixes and improvements");

        BulletText("Changed some clickable pointers to be right-aligned.");
    }

    private void Draw0_1_3_7()
    {
        if (!DrawVersionHeader(0, 1, 3, 7, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("Inter-Plugin API");

        BulletText("Added ImGuiDrawPointer v3 and equivalent Get...Delegate IPC functions.");
        BulletText("The API version is now 1.6.");
    }

    private void Draw0_1_3_6()
    {
        if (!DrawVersionHeader(0, 1, 3, 6, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("Bug fixes and improvements");

        BulletText("Started allowing a 0x prefix in front of the address in the /dynamis inspect command.");
        BulletText("Fixed an issue with generic type resolution.");
    }

    private void Draw0_1_3_5()
    {
        if (!DrawVersionHeader(0, 1, 3, 5, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("New features");

        BulletText("Added a setting to silence data.yml-related errors.");
    }

    private void Draw0_1_3_3()
    {
        if (!DrawVersionHeader(0, 1, 3, 3, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("Miscellaneous");

        BulletText("Updated for Dalamud 13.");
    }

    private void Draw0_1_3_2()
    {
        if (!DrawVersionHeader(0, 1, 3, 2, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("Inter-Plugin API");

        BulletText("Added ImGuiDrawPointer v2 and equivalent Get...Delegate IPC functions.");
        BulletText("The API version is now 1.5.");
    }

    private void Draw0_1_3_1()
    {
        if (!DrawVersionHeader(0, 1, 3, 1, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("Bug fixes and improvements");

        BulletText("Fixed a bug with generic type parsing.");
        BulletText("Improved handling of unsupported type in the pointer thunk generator.");
    }

    private void Draw0_1_3_0()
    {
        if (!DrawVersionHeader(0, 1, 3, 0, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("New features");

        BulletText("Added support for generic types from ClientStructs.");
        BulletText("Added an option to pass a name to the Show-Object cmdlet.");

        ImGuiComponents.SeparatorText("Inter-Plugin API");

        BulletText("Added InspectObject v2 and InspectRegion v2 IPC functions.");
        BulletText("The API version is now 1.4.");

        ImGuiComponents.SeparatorText("Bug fixes and improvements");

        BulletText("Improved handling of class pre-identification.");
        BulletText("Improved IPC thread safety.");
        BulletText("Improved some wording in the Object Inspector.");
        BulletText(
            "Prevented double-initialization of the Symbol Handler in case of plugin restart (causing lag spikes)."
        );
        BulletText("Improved the function disassembler's end detection logic.");
        BulletText("Fixed more IPFD initialization issues.");
        BulletText("Fixed parsing of some information from ClientStructs' data.yml.");
        BulletText("Improved handling of nested fields in the Object Inspector.");
        BulletText("Improved hex viewer annotated mode.");
    }

    private void Draw0_1_2_1()
    {
        if (!DrawVersionHeader(0, 1, 2, 1, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("Bug fixes and improvements");

        BulletText(
            "Handled ref types in the generated pointer thunks supporting the Object Inspector and the hosted PowerShell."
        );
        BulletText("Fixed a crash related to IPFD initialization.");
    }

    private void Draw0_1_2_0()
    {
        if (!DrawVersionHeader(0, 1, 2, 0, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("New features");

        BulletText("Revamped the Settings window.");

        ImGuiComponents.SeparatorText("Bug fixes and improvements");

        BulletText("Added new interface settings: Serious mode, and automatic annotated mode for hex viewers.");
        BulletText("Improved the class identification logic.");
    }

    private void Draw0_1_1_0()
    {
        if (!DrawVersionHeader(0, 1, 1, 0, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("New features");

        BulletText(
            "Hex viewers now have an annotated mode, with 8 bytes per row followed by the fields they contain."
        );
        BulletText(
            "Array size is now taken into account for pointer fields that have a similarly-named, span-typed property."
        );
        BulletText(
            "IPFD breakpoints can now be configured to only keep a single thread snapshot per instruction address and/or type of the \"this\" argument."
        );

        ImGuiComponents.SeparatorText("Bug fixes and improvements");

        BulletText(
            "Made some tooltips that explain why buttons are disabled only appear when they actually are disabled."
        );
        BulletText(
            "Moved the PowerShell layer on the plugin icon to prevent it from being occluded by Dalamud's icon overlays."
        );

        ImGuiComponents.SeparatorText("Miscellaneous");

        BulletText("Added documentation in the GitHub repository.");
    }

    private void Draw0_1_0_0()
    {
        if (!DrawVersionHeader(0, 1, 0, 0, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("New features");

        BulletText("Added a hosted PowerShell.");
        BulletText(
            "Two \"editions\" of the plugin are now distributed through the repository. One includes everything, the other excludes the hosted PowerShell at compile time."
        );
        BulletText(
            "Various cmdlets, ad hoc types and other infrastructure are provided to the hosted PowerShell, to facilitate interacting with native objects and Dalamud services."
        );
        BulletText(
            "The Symbol Handler can now be turned off on Windows as well, and also has a Force Initialize mode, equivalent to how it operates on Wine."
        );

        ImGuiComponents.SeparatorText("Inter-Plugin API");

        BulletText("Added GetApiVersion IPC function.");
        BulletText("Added ApiInitialized and ApiDisposed IPC events.");
        BulletText(
            "The API version is now 1.3. (Previous versions could not be told apart except through feature detection.)"
        );

        ImGuiComponents.SeparatorText("Bug fixes and improvements");

        BulletText("Made the Resource Handle inspector more robust.");
    }

    private void Draw0_0_1_15()
    {
        if (!DrawVersionHeader(0, 0, 1, 15, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("Bug fixes and improvements");

        BulletText("Fixed an address miscalculation related to EXE ASLR.");
    }

    private void Draw0_0_1_14()
    {
        if (!DrawVersionHeader(0, 0, 1, 14, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("Inter-Plugin API");

        BulletText("Added GetClass, IsInstanceOf and PreloadDataYaml IPC functions.");

        ImGuiComponents.SeparatorText("Bug fixes and improvements");

        BulletText("Added various \"Copy to clipboard\" buttons.");
    }

    private void Draw0_0_1_13()
    {
        if (!DrawVersionHeader(0, 0, 1, 13, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("New features");

        BulletText("The Toolbox and Settings windows now display Dynamis's version.");
        BulletText("The Symbol Handler can now be turned off on Wine.");
    }

    private void Draw0_0_1_12()
    {
        if (!DrawVersionHeader(0, 0, 1, 12, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("Bug fixes and improvements");

        BulletText("EXE ASLR is now actually handled (since 0.0.1.11).");
        BulletText("Fixed various crashes related to EXE ASLR.");
        BulletText("Fixed other Object Inspector bugs.");
    }

    private void Draw0_0_1_5()
    {
        if (!DrawVersionHeader(0, 0, 1, 5, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("New features");

        BulletText(
            "Thread snapshots from IPFD breakpoints now have stack frame information, and their stacks are organized by frame, when possible."
        );
        BulletText("Basic information is now displayed about objects from libraries (such as Direct3D and DXGI).");
        BulletText(
            "ClientStructs' data.yml can now be fetched from the project's GitHub repository and managed automatically."
        );

        ImGuiComponents.SeparatorText("Bug fixes and improvements");

        BulletText("Improved the function disassembler's end detection logic.");
        BulletText("Fixed a bug when hovering snapshots.");

        ImGuiComponents.SeparatorText("Miscellaneous");

        BulletText("Updated for Dalamud 12.");
        BulletText("Updated to .NET 9.");
    }

    private void Draw0_0_1_4()
    {
        if (!DrawVersionHeader(0, 0, 1, 4, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("New features");

        BulletText("The Object Inspector can now process multiple inheritance / interface pointers.");
        BulletText(
            "Symbols can now be resolved (when supported by the underlying platform): addresses can now be turned into the form foo.exe!DoTheThing+0x1234."
        );
    }

    private void Draw0_0_1_3()
    {
        if (!DrawVersionHeader(0, 0, 1, 3, 0)) {
            return;
        }

        ImGuiComponents.SeparatorText("Initial release");

        BulletText("Previous versions had to be built from source.");
        BulletText("This is the first release distributed through a repository.");
    }
}
