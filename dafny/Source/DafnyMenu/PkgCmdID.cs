// PkgCmdID.cs
// MUST match PkgCmdID.h
using System;

namespace DafnyLanguage.DafnyMenu
{
  static class PkgCmdIDList
  {
    public const uint DafnyMenu = 0x1020;

    public const uint DafnyMenuGroup    = 0x1021;
    public const uint TacnyMenuGroup    = 0x1022;
    public const uint TacnyContextGroup = 0x1023;
    
    public const uint cmdidCompile                    = 0x100;
    public const uint cmdidRunVerifier                = 0x101;
    public const uint cmdidStopVerifier               = 0x102;
    public const uint cmdidToggleSnapshotVerification = 0x103;
    public const uint cmdidToggleBVD                  = 0x104;
    public const uint cmdidToggleMoreAdvancedSnapshotVerification = 0x105;
    public const uint cmdidToggleAutomaticInduction   = 0x106;
    public const uint cmdidDiagnoseTimeouts           = 0x107;
    
    public const uint cmdidExpandTactics    = 0x0108;
    public const uint cmdidExpandRot        = 0x0109;
    public const uint cmdidExpandAllTactics = 0x010a;
    public const uint cmdidToggleTacny      = 0x010b;
    
    public const uint cmdidContextExpandTactics = 0x010c;
    public const uint cmdidContextExpandRot     = 0x010d;
  };
}