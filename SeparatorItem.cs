namespace NotificationIcon.NET;

public record class SeparatorItem : MenuItem
{
    private const string MAGIC_TEXT = "-";

    public SeparatorItem() : base(MAGIC_TEXT)
    { }
}