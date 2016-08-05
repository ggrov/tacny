// PkgCmdID.cs
// MUST match PkgCmdID.h
using System;

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
	
    public const uint DafnyMenu = 0x1020;
    public const uint TacnyMenuGroup = 0x1022;
    public const uint TacnyContextGroup = 0x1023;
        
    public const uint cmdidExpandTactics = 0x108;
    public const uint cmdidExpandRot = 0x109;
    public const uint cmdidExpandAllTactics = 0x10a;
    public const uint cmdidToggleTacny = 0x10b;
    
    public const uint cmdidContextExpandTactics = 0x10c;
    public const uint cmdidContextExpandRot = 0x10d;
  };
}