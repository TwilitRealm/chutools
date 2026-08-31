namespace JaiSeqX.JAI.Seq;

public enum JaiSeqOpcodeV2 : byte
{
    OpenTrack = 0xC1,
    Jmp = 0xC7,
    JmpF = 0xC8,
    RegLoad = 0xD8,
    Reg = 0xD9,
    Tempo = 0xE0,
    Bank = 0xE2,
    Prg = 0xE3,
    Wait = 0xF0,
    WaitByte = 0xF1,
    Finish = 0xFF,
}

public static class JaiSeqOpcodeV2Extensions
{
    extension(BinaryWriter writer)
    {
        public void Write(JaiSeqOpcodeV2 opcode)
        {
            writer.Write((byte)opcode);
        }
    }
}