namespace TheGlitch
{
    public interface IQuickHackable
    {
        // 返回四方向的选项（可以为 null）
        void GetQuickHacks(out QuickHackOption up, out QuickHackOption right, out QuickHackOption down, out QuickHackOption left);
    }
}

