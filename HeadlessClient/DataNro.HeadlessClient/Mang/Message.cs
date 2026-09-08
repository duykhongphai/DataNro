namespace DataNro.Mang;

public class Message
{
    public sbyte command;

    /// <summary>Dữ liệu gốc của gói nhận được, giữ lại để có thể đọc lại từ đầu.</summary>
    public sbyte[] raw;

    private readonly myReader dis;
    private readonly myWriter dos;

    public Message(int command)
    {
        this.command = (sbyte)command;
        dos = new myWriter();
    }

    public Message()
    {
        dos = new myWriter();
    }

    public Message(sbyte command)
    {
        this.command = command;
        dos = new myWriter();
    }

    public Message(sbyte command, sbyte[] data)
    {
        this.command = command;
        raw = data;
        dis = new myReader(data);
    }

    public sbyte[] getData() => dos?.getData();

    public myReader reader() => dis;

    public myWriter writer() => dos;

    /// <summary>Đọc 4 byte thành số nguyên - giữ tên gốc để đối chiếu dễ.</summary>
    public int readInt3Byte() => dis.readInt();

    public int RawLength => raw?.Length ?? 0;

    /// <summary>Một bộ đọc mới trên dữ liệu gốc, dùng cho các bộ lắng nghe bên ngoài.</summary>
    public myReader freshReader() => raw != null ? new myReader(raw) : null;

    public void cleanup()
    {
    }
}
