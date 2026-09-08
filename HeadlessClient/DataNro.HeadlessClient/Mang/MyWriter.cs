using System.Text;

namespace DataNro.Mang;

public class myWriter
{
    public sbyte[] buffer = new sbyte[2048];
    private int posWrite;
    private int lenght = 2048;

    public myWriter() { }
    public myWriter(int len) { buffer = new sbyte[len]; lenght = len; }

    public void writeSByte(sbyte value)
    {
        checkLenght(0);
        buffer[posWrite++] = value;
    }

    public void writeSByteUncheck(sbyte value) { buffer[posWrite++] = value; }

    public void writeByte(sbyte value) => writeSByte(value);
    public void writeByte(int value) => writeSByte((sbyte)value);

    public void writeChar(char value)
    {
        writeSByte(0);
        writeSByte((sbyte)value);
    }

    public void writeUnsignedByte(byte value) => writeSByte((sbyte)value);

    public void writeUnsignedByte(byte[] value)
    {
        checkLenght(value.Length);
        for (int i = 0; i < value.Length; i++) writeSByteUncheck((sbyte)value[i]);
    }

    public void writeSByte(sbyte[] value)
    {
        checkLenght(value.Length);
        for (int i = 0; i < value.Length; i++) writeSByteUncheck(value[i]);
    }

    public void writeShort(short value)
    {
        checkLenght(2);
        for (int n = 1; n >= 0; n--) writeSByteUncheck((sbyte)(value >> n * 8));
    }

    public void writeShort(int value)
    {
        checkLenght(2);
        short s = (short)value;
        for (int n = 1; n >= 0; n--) writeSByteUncheck((sbyte)(s >> n * 8));
    }

    public void writeUnsignedShort(ushort value)
    {
        checkLenght(2);
        for (int n = 1; n >= 0; n--) writeSByteUncheck((sbyte)(value >> n * 8));
    }

    public void writeInt(int value)
    {
        checkLenght(4);
        for (int n = 3; n >= 0; n--) writeSByteUncheck((sbyte)(value >> n * 8));
    }

    public void writeLong(long value)
    {
        checkLenght(8);
        for (int n = 7; n >= 0; n--) writeSByteUncheck((sbyte)(value >> n * 8));
    }

    public void writeBoolean(bool value) => writeSByte((sbyte)(value ? 1 : 0));
    public void writeBool(bool value) => writeSByte((sbyte)(value ? 1 : 0));

    public void writeString(string value)
    {
        char[] array = value.ToCharArray();
        writeShort((short)array.Length);
        checkLenght(array.Length);
        for (int i = 0; i < array.Length; i++) writeSByteUncheck((sbyte)array[i]);
    }

    public void writeUTF(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writeShort((short)bytes.Length);
        checkLenght(bytes.Length);
        for (int i = 0; i < bytes.Length; i++) writeSByteUncheck((sbyte)bytes[i]);
    }

    public void write(ref sbyte[] data, int arg1, int arg2)
    {
        if (data == null) return;
        for (int i = 0; i < arg2; i++)
        {
            writeSByte(data[i + arg1]);
            if (posWrite > buffer.Length) break;
        }
    }

    public void write(sbyte[] value) => writeSByte(value);

    public sbyte[] getData()
    {
        if (posWrite <= 0) return null;
        sbyte[] array = new sbyte[posWrite];
        for (int i = 0; i < posWrite; i++) array[i] = buffer[i];
        return array;
    }

    public void checkLenght(int ltemp)
    {
        if (posWrite + ltemp > lenght)
        {
            sbyte[] array = new sbyte[lenght + 1024 + ltemp];
            for (int i = 0; i < lenght; i++) array[i] = buffer[i];
            buffer = array;
            lenght += 1024 + ltemp;
        }
    }

    public void Close() { buffer = null; }
    public void close() { buffer = null; }
}
