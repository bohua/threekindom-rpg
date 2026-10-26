namespace ThreeKingdoms.Gameplay.Story
{
    // The 鞭督邮 story poses (user 2026-10-26; Codex art/v02-duyou-anim, eight per character, drawn facing screen right).
    // Indices into each character's own row; DuyouPoseImporter appends the rows to the atlases.
    public static class DuyouPose
    {
        public const int LiuRequest = 0, LiuFist = 1, LiuDrag = 2, LiuCaneRaise = 3, LiuCaneStrike = 4, LiuHangSeal = 5, LiuStop = 6, LiuLookBack = 7;
        public const int ZhangAngry = 0, ZhangCharge = 1, ZhangDragHair = 2, ZhangWhipRaise = 3, ZhangWhipStrike = 4, ZhangHeldBack = 5, ZhangLaugh = 6, ZhangArmsCrossed = 7;
        public const int GuanStroke = 0, GuanHandOnBlade = 1, GuanNod = 2, GuanWatch = 3, GuanSalute = 4, GuanTurn = 5, GuanPoint = 6, GuanSigh = 7;
        public const int DuLounge = 0, DuWaveAway = 1, DuDragged = 2, DuBound = 3, DuFlinch = 4, DuBeg = 5, DuSealed = 6, DuShout = 7;
    }
}
