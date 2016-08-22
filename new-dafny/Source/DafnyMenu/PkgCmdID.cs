// PkgCmdID.cs
// MUST match PkgCmdID.h
using System;
using Microsoft.VisualStudio.Shell;

namespace DafnyLanguage.DafnyMenu
{
  static class PkgCmdIDList
  {
    public const uint cmdidCompile = 0x10;
    public const uint cmdidRunResolver = 0x11;
    public const uint cmdidStopResolver = 0x12;
    public const uint cmdidRunVerifier = 0x101;
    public const uint cmdidStopVerifier = 0x102;
    public const uint cmdidMenu = 0x1021;
    public static uint cmdidToggleSnapshotVerification = 0x103;
    public const uint cmdidToggleBVD = 0x104;
    public static uint cmdidToggleMoreAdvancedSnapshotVerification = 0x105;
    public static uint cmdidToggleAutomaticInduction = 0x106;
    public static uint cmdidDiagnoseTimeouts = 0x107;
    public const uint cmdidToggleDeadCode = 0x108;
	
    public const uint DafnyMenu = 0x1020;
    public const uint RefactoringMenu = 0x1021;
    public const uint RefactoringContextMenu = 0x1022;

    public const uint RefactoringMenuGroup = 0x1020;
    public const uint RefactoringContextGroup = 0x1021;
        
    public const uint cmdidExpandAllTactics = 0x100;
    public const uint cmdidToggleTacny = 0x101;
    public const uint cmdidRemoveAllDeadCode = 0x102;
    
    public const uint cmdidContextExpandTactics = 0x110;
    public const uint cmdidContextExpandRot = 0x111;
    public const uint cmdidContextRemoveDeadCode = 0x112;
    public const uint cmdidContextRemoveDeadMemberCode = 0x113;
  }
}