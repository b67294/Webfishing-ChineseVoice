using System.Reflection;
using GDWeave;
using GDWeave.Godot;
using GDWeave.Modding;

namespace Melon.ChinesePinyinVoice;

public sealed class Mod : IMod {
    public Mod(IModInterface modInterface) {
        var modDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppContext.BaseDirectory;

        var pinyinData = PinyinData.Load(modDirectory);
        modInterface.RegisterScriptMod(new PlayerVoiceScriptMod(pinyinData));
        modInterface.RegisterScriptMod(new SpeechBubbleFontScriptMod());
        modInterface.Logger.Information("Chinese Pinyin Voice loaded with {Count} mapped Chinese characters.", pinyinData.Count);
    }

    public void Dispose() {
    }
}

internal sealed class SpeechBubbleFontScriptMod : IScriptMod {
    private const string SpeechBubbleScriptPath = "res://Scenes/Entities/Player/SpeechBubble/speech_bubble.gdc";

    public bool ShouldRun(string path) => path == SpeechBubbleScriptPath;

    public IEnumerable<Token> Modify(string path, IEnumerable<Token> tokens) {
        var insertedHelpers = false;
        var insertedApplyCall = false;
        var insertedData = false;

        var helperInsertWaiter = new MultiTokenWaiter([
            token => token.Type is TokenType.PrVar,
            token => token is IdentifierToken { Name: "speed" },
            token => token.Type is TokenType.OpAssign,
            token => token is ConstantToken,
            token => token.Type is TokenType.Newline
        ], allowPartialMatch: true);

        var dataInsertWaiter = new MultiTokenWaiter([
            token => token.Type is TokenType.PrVar,
            token => token is IdentifierToken { Name: "final_text" },
            token => token.Type is TokenType.OpAssign,
            token => token is ConstantToken,
            token => token.Type is TokenType.Newline
        ], allowPartialMatch: true);

        var applyCallWaiter = new MultiTokenWaiter([
            token => token.Type is TokenType.Dollar,
            token => token is IdentifierToken { Name: "RichTextLabel" },
            token => token.Type is TokenType.Period,
            token => token is IdentifierToken { Name: "text" },
            token => token.Type is TokenType.OpAssign,
            token => token is IdentifierToken { Name: "final_text" },
            token => token.Type is TokenType.Newline
        ], allowPartialMatch: true);

        foreach (var token in tokens) {
            if (!insertedData && dataInsertWaiter.Check(token)) {
                insertedData = true;
                yield return token;

                foreach (var injected in GdTokenTools.TokenizeLines(BuildDataScript())) {
                    yield return injected;
                }

                continue;
            }

            if (!insertedHelpers && helperInsertWaiter.Check(token)) {
                insertedHelpers = true;
                yield return token;

                foreach (var injected in GdTokenTools.TokenizeLines(BuildHelperFunctions())) {
                    yield return injected;
                }

                continue;
            }

            if (!insertedApplyCall && applyCallWaiter.Check(token)) {
                insertedApplyCall = true;
                yield return token;

                foreach (var injected in GdTokenTools.TokenizeLines("\tcnpv_apply_chinese_bubble_font()")) {
                    yield return injected;
                }

                continue;
            }

            yield return token;
        }
    }

    private static string BuildDataScript() {
        return """
var cnpv_original_text = ""
""";
    }

    private static string BuildHelperFunctions() {
        return string.Join('\n',
            "func cnpv_bubble_has_chinese(text):",
            "\tfor i in range(text.length()):",
            "\t\tvar code = text.ord_at(i)",
            "\t\tif code >= 19968 && code <= 40959: return true",
            "\treturn false",
            "",
            "func cnpv_strip_bubble_breaks(text):",
            "\tvar out = \"\"",
            "\tvar i = 0",
            "\twhile i < text.length():",
            "\t\tif i + 1 < text.length() && text[i] == \"\\\\\" && text[i + 1] == \"n\":",
            "\t\t\ti += 2",
            "\t\t\tcontinue",
            "\t\tif text[i] == \"\\n\":",
            "\t\t\ti += 1",
            "\t\t\tcontinue",
            "\t\tout += text[i]",
            "\t\ti += 1",
            "\treturn out",
            "",            "func cnpv_wrap_chinese_bubble_text(text):",
            "\tvar clean = cnpv_strip_bubble_breaks(text)",
            "\tvar out = \"\"",
            "\tvar width = 0",
            "\tfor i in range(clean.length()):",
            "\t\tvar letter = clean[i]",
            "\t\tvar code = clean.ord_at(i)",
            "\t\tvar letter_width = 1",
            "\t\tif code >= 19968 && code <= 40959: letter_width = 2",
            "\t\tif width > 0 && width + letter_width > 20:",
            "\t\t\twidth = 0",
            "\t\tout += letter",
            "\t\twidth += letter_width",
            "\treturn out",
            "",
            "func cnpv_apply_chinese_bubble_font():",
            "\tif cnpv_bubble_has_chinese(final_text) == false: return",
            "\tfinal_text = cnpv_wrap_chinese_bubble_text(final_text)",
            "\tcnpv_original_text = final_text",
            "\tvar font_data = DynamicFontData.new()",
            "\tfont_data.font_path = \"res://Assets/Themes/unifont-16.0.01.otf\"",
            "\tvar font = DynamicFont.new()",
            "\tfont.font_data = font_data",
            "\tfont.size = 28",
            "\tfont.extra_spacing_char = -2",
            "\tfont.use_filter = false",
            "\t$RichTextLabel.add_font_override(\"font\", font)",
            "\t$RichTextLabel.add_color_override(\"font_color\", Color(0.20, 0.12, 0.06, 1.00))",
            "\t$RichTextLabel.text = final_text",
            ""
        );
    }
}

internal sealed record PinyinData(string Chars, string Syllables) {
    public int Count => string.IsNullOrEmpty(Syllables) ? 0 : Syllables.Count(c => c == ',') + 1;

    public static PinyinData Load(string modDirectory) {
        var charsPath = Path.Combine(modDirectory, "pinyin_chars.txt");
        var syllablesPath = Path.Combine(modDirectory, "pinyin_syllables.txt");

        if (!File.Exists(charsPath) || !File.Exists(syllablesPath)) {
            throw new FileNotFoundException("Chinese Pinyin Voice is missing pinyin data files.");
        }

        return new PinyinData(
            File.ReadAllText(charsPath).Trim(),
            File.ReadAllText(syllablesPath).Trim()
        );
    }
}

internal sealed class PlayerVoiceScriptMod(PinyinData pinyinData) : IScriptMod {
    private const string PlayerScriptPath = "res://Scenes/Entities/Player/player.gdc";

    public bool ShouldRun(string path) => path == PlayerScriptPath;

    public IEnumerable<Token> Modify(string path, IEnumerable<Token> tokens) {
        var insertedData = false;
        var replacedSyncTalk = false;
        var possibleFunction = false;
        var skippingSyncTalk = false;
        var functionBuffer = new List<Token>();

        var dataInsertWaiter = new MultiTokenWaiter([
            token => token.Type is TokenType.PrVar,
            token => token is IdentifierToken { Name: "int_text" },
            token => token.Type is TokenType.OpAssign,
            token => token is ConstantToken,
            token => token.Type is TokenType.Newline
        ], allowPartialMatch: true);

        foreach (var token in tokens) {
            if (!insertedData && dataInsertWaiter.Check(token)) {
                insertedData = true;
                yield return token;

                foreach (var injected in GdTokenTools.TokenizeLines(BuildDataScript())) {
                    yield return injected;
                }

                continue;
            }

            if (replacedSyncTalk && !skippingSyncTalk) {
                yield return token;
                continue;
            }

            if (skippingSyncTalk) {
                if (!possibleFunction) {
                    if (token.Type is TokenType.PrFunction) {
                        possibleFunction = true;
                        functionBuffer.Clear();
                        functionBuffer.Add(token);
                    }

                    continue;
                }

                functionBuffer.Add(token);
                if (token is IdentifierToken { Name: "_talk" }) {
                    skippingSyncTalk = false;
                    possibleFunction = false;

                    foreach (var buffered in functionBuffer) {
                        yield return buffered;
                    }
                } else if (token is IdentifierToken) {
                    possibleFunction = false;
                    functionBuffer.Clear();
                }

                continue;
            }

            if (!possibleFunction) {
                if (token.Type is TokenType.PrFunction) {
                    possibleFunction = true;
                    functionBuffer.Clear();
                    functionBuffer.Add(token);
                    continue;
                }

                yield return token;
                continue;
            }

            functionBuffer.Add(token);
            if (token is IdentifierToken { Name: "_sync_talk" }) {
                replacedSyncTalk = true;
                skippingSyncTalk = true;
                possibleFunction = false;
                functionBuffer.Clear();

                foreach (var injected in GdTokenTools.TokenizeLines(BuildReplacementFunctions())) {
                    yield return injected;
                }

                continue;
            }

            if (token is IdentifierToken) {
                possibleFunction = false;
                foreach (var buffered in functionBuffer) {
                    yield return buffered;
                }

                functionBuffer.Clear();
            }
        }
    }

    private string BuildDataScript() {
        return $"""
var cnpv_chars = "{pinyinData.Chars}"
var cnpv_pinyin = "{pinyinData.Syllables}".split(",")
""";
    }

    private static string BuildReplacementFunctions() {
        return string.Join('\n',
            "func cnpv_pinyin_for(letter):",
            "\tvar i = cnpv_chars.find(letter)",
            "\tif i < 0: return \"\"",
            "\tif i >= cnpv_pinyin.size(): return \"\"",
            "\treturn cnpv_pinyin[i]",
            "",
            "func _sync_talk(letter):",
            "\tif PlayerData.players_muted.has(owner_id): return",
            "\tvar voice_letters = \"abcdefghijklmnopqrstuvwxyz\"",
            "\tvar voice_text = \"\"",
            "\tvar lowered = letter.to_lower()",
            "\tif voice_letters.find(lowered) >= 0:",
            "\t\tvoice_text = lowered",
            "\telse:",
            "\t\tvoice_text = cnpv_pinyin_for(letter)",
            "\tif voice_text == \"\": return",
            "\tfor i in range(voice_text.length()):",
            "\t\tvar voice_letter = voice_text[i].to_lower()",
            "\t\tif voice_letters.find(voice_letter) < 0: continue",
            "\t\tanimation_data[\"talking\"] = 0.40",
            "\t\t_talk(voice_letter, PlayerData.voice_pitch)",
            "\t\tNetwork._send_actor_action(actor_id, \"_talk\", [voice_letter, PlayerData.voice_pitch], false, Network.CHANNELS.SPEECH)",
            ""
        );
    }

}

internal static class GdTokenTools {
    public static IEnumerable<Token> TokenizeLines(string gdScript) {
        foreach (var rawLine in gdScript.Replace("\r\n", "\n").Split('\n')) {
            if (rawLine.Length == 0) {
                yield return new Token(TokenType.Newline, 0);
                continue;
            }

            var indent = 0u;
            while (indent < rawLine.Length && rawLine[(int)indent] == '\t') {
                indent++;
            }

            yield return new Token(TokenType.Newline, indent);

            var line = rawLine[(int)indent..];
            foreach (var token in ScriptTokenizer.Tokenize(line)) {
                if (token.Type is TokenType.Newline) {
                    continue;
                }

                yield return token;
            }
        }

        yield return new Token(TokenType.Newline, 0);
    }
}


