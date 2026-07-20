namespace GameUp.SDK
{
    public interface IAdCondition
    {
        bool CanShow(AdUnitType adType, string where, out string reason);
        string GetString();
    }
}