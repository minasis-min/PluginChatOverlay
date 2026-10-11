using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private void OnCommand(string _, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "config": OpenConfig(); break;
            case "on": settings.Enabled = true; Save(); break;
            case "off": settings.Enabled = false; Save(); break;
            case "reset": ClearHistory(); break;
            case "test": TestMessages(); break;
            case "unlink": chat.RemoveChatLinkHandler(701); testLinkActive = false; break;
            case "burst": for (var i = 0; i < 2000; i++) chat.Print($"[PCO 負荷試験] {i} 日本語 折り返し表示の確認"); break;
            default: OpenMain(); break;
        }
    }
    private void TestMessages()
    {
        if (testLinkActive) chat.RemoveChatLinkHandler(701);
        var link = chat.AddChatLinkHandler(701, (_, _) => { testLinkClicks++; OpenConfig(); });
        testLinkActive = true;
        chat.Print("PCO同文テスト");
        chat.Print("日本語・タグ付きPrint", "PluginChatOverlay");
        chat.PrintError("日本語エラー・PrintError");
        chat.Print(new XivChatEntry { Type = XivChatType.Say, Name = new SeString(new TextPayload("PCOテスト送信者")), Message = new SeString(new TextPayload("任意種別Say・プラグイン出力")) });
        var text = new SeStringBuilder().AddText("日本語リンク試験: ")
            .Add(link).AddUiForeground(45).AddText("クリックして設定を開く")
            .AddUiForegroundOff().Add(RawPayload.LinkTerminator).AddText(" 長い文章を挟んで、狭いウィンドウで折り返しと色の隣接を確認してください。 ")
            .Add(link).AddText("二つ目の設定リンク").Add(RawPayload.LinkTerminator).Build();
        chat.Print(text);
    }
}
