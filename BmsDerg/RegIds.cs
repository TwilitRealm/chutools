namespace BmsDerg;

public static class RegIds
{
    public static string GetRegName(uint id)
    {
        return id switch
        {
            >= 0x40 and <= 0x4f => $"p{id - 0x40}",
            0x60 => "rChildStatus",
            0x61 => "rLoopCount",
            0x62 => "rTimeBase",
            0x63 => "rTranspose",
            0x64 => "rBendSense",
            0x65 => "rGateRate",
            0x66 => "rSkipSample",
            0x67 => "rBankNumber",
            0x68 => "rProgNumber",
            0x69 => "rPanPower",
            0x6a => "rReleaseNoteOnPrioBugged",
            0x6b => "rNoteOnPrio",
            0x6c => "rReleasePrio",
            0x6d => "rDirectRelease",
            0x6e => "rVibDepth",
            0x6f => "rVibDepthPrecise",
            0x70 => "rTremDepth",
            0x71 => "rVibPitch",
            0x72 => "rTremPitch",
            0x73 => "rVibDelay",
            0x74 => "rTremDelay",
            _ => $"r{id}"
        };
    }
}