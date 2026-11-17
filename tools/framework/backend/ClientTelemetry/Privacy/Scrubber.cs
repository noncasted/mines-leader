using System.Text.RegularExpressions;

namespace Game.ClientTelemetry;

// Cleans the text of a client error, the only free text of the telemetry. What is left: the type of the exception, the
// template of the message, the names of methods. In doubt it cuts, and a key that promises a secret or a name takes the
// rest of its line with it: such a value may hold spaces. A name written as a plain word ("Player Ivan left") cannot be
// told from the template; see CONTRACT.md.
//
// The rules are plain regular expressions without lookaround, the client repeats them in C# 9 (without NonBacktracking,
// which Unity does not have) and is checked by the same corpus. NonBacktracking here: the input comes from a public endpoint.
public static class Scrubber
{
    private const RegexOptions _options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;
    private const RegexOptions _ignoreCase = _options | RegexOptions.IgnoreCase;
    private const int _maxPlainWordLength = 32;
    private const int _maxUpperWordWithDigitsLength = 12;
    private const int _maxWordWithDigitsLength = 24;
    private const int _minHexWordLength = 8;
    private const int _maxShortDigitRun = 2;

    private const string _secretKeys = @"token|password|passwd|pwd|secret|api[_\-]?key|authorization|cookie|credentials?|init_?data|tgwebappdata";
    private const string _personalKeys = @"name|user|username|login|e?mail|phone|nick|nickname|player|account|address";
    private const string _domainZones = "com|net|org|ru|su|ua|by|kz|de|uk|fr|cn|jp|br|io|me|app|dev|xyz|gg|co|info|tv|site|online|lan|local|internal|example";
    private const string _fileExtensions = "txt|json|dat|png|jpg|jpeg|pdf|csv|xml|cfg|ini|sqlite|bak|zip|js|wasm|cs";

    private static readonly Regex _url = new(@"[a-zA-Z][a-zA-Z0-9+.\-]*://[^\s?#]*(?:[?#][^\r\n]*)?", _options);
    private static readonly Regex _urlWithoutHost = new(@"\b(?:data|blob|javascript|about|mailto|tel|sms|tg):[^\s]*", _options);
    private static readonly Regex _jwt = new(@"\beyJ[A-Za-z0-9_\-]+(?:\.[A-Za-z0-9_\-]+)*", _options);
    private static readonly Regex _authScheme = new(@"\b(bearer|basic)\s+[A-Za-z0-9+/=._\-]{4,}", _ignoreCase);
    private static readonly Regex _secretValue = new(@"([\w.\-]*(?:" + _secretKeys + @"))[\\""']*\s*[:=]\s*([^\r\n]*)", _ignoreCase);
    private static readonly Regex _personalValue = new(@"([\w.\-]*(?:" + _personalKeys + @"))[\\""']*\s*[:=]\s*([^\r\n]*)", _ignoreCase);
    private static readonly Regex _queryAfterDelimiter = new(@"[?#&][\w.\-%]+=[^\r\n]*", _options);
    private static readonly Regex _queryOfPairs = new(@"[\w.\-%]+=[^&\s]*&[^\r\n]*", _options);
    private static readonly Regex _pathWithQuery = new(@"\S*[/\\]\S*[?#][^\r\n]*", _options);
    private static readonly Regex _email = new(@"[^\s<>()\[\],;:""']*@\S*", _options);
    private static readonly Regex _guid = new(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", _options);
    private static readonly Regex _ipV4 = new(@"\b[0-9]{1,3}(?:\.[0-9]{1,3}){3}\b", _options);
    private static readonly Regex _ipV6 = new(@"(^|[^0-9A-Za-z_:.<])((?:[0-9a-fA-F]{1,4})?(?::[0-9a-fA-F]{0,4}){2,7}(?:%\w+)?)", _options | RegexOptions.Multiline);
    private static readonly Regex _percentEncoded = new(@"\S*%[0-9a-fA-F]{2}\S*", _options);
    private static readonly Regex _base64 = new(@"[A-Za-z0-9+/]{8,}={1,2}(\s|[,;)\]}]|$)", _options | RegexOptions.Multiline);
    private static readonly Regex _doubleQuoted = new(@"""[^""\r\n]*""?", _options);
    private static readonly Regex _singleQuoted = new(@"(^|[^\w])'[^'\r\n]*'?", _options | RegexOptions.Multiline);
    private static readonly Regex _backticked = new(@"(^|[^\w])`[^`\r\n]*`", _options | RegexOptions.Multiline);
    private static readonly Regex _braced = new(@"\{[^{}\r\n]*\}", _options);
    private static readonly Regex _angledWithSpace = new(@"<[^<>\r\n]*\s[^<>\r\n]*>", _options);
    private static readonly Regex _typographicQuoted = new(@"[«“‘„][^»”’\r\n]*[»”’]?", _options);

    // Everything out of printable ASCII: a text of a player or a localized one, and the NUL that Postgres cannot store.
    private static readonly Regex _notPrintableAscii = new(@"[^\x09\x0A\x0D\x20-\x7E]+", _options);
    private static readonly Regex _absolutePath = new(@"(^|[\s(\[=,:])(?:[A-Za-z]:[\\/]|[\\/]{1,2}[^\s\\/]|~[\\/])[^\r\n]*", _options | RegexOptions.Multiline);
    private static readonly Regex _relativePath = new(@"[^\s()\[\],;""']*[/\\][^\s()\[\],;""']*", _options);

    // Outer/Inner:Method: a nested type in a Unity stack, the only thing with a slash that is kept.
    private static readonly Regex _nestedTypeFrame = new(@"\A[\w.<>`$+\-]+(?:/[\w.<>`$+\-]+)+:[A-Za-z_<.][\w.<>`$]*\z", _options);
    private static readonly Regex _keyValue = new(@"([\w.\-]+)\s*=\s*[^=>\s,;&)\]}][^\s,;&)\]}]*", _options);
    private static readonly Regex _hostWithPort = new(@"\b[a-zA-Z0-9\-]+(?:\.[a-zA-Z0-9\-]+)+:[0-9]+", _options);

    // System.Net and System.IO are names of code: a domain is all lower or all upper case.
    private static readonly Regex _domain = new(@"\b[a-z0-9\-]+(?:\.[a-z0-9\-]+)*\.(?:" + _domainZones + @")\b", _options);
    private static readonly Regex _upperDomain = new(@"\b[A-Z0-9\-]+(?:\.[A-Z0-9\-]+)*\.(?:" + _domainZones.ToUpperInvariant() + @")\b", _options);
    private static readonly Regex _wwwHost = new(@"\bwww\.\S*", _ignoreCase);
    private static readonly Regex _fileName = new(@"[\w\-.]+\.(?:" + _fileExtensions + @")\b", _options);

    // A backtick with digits is the arity of a generic type (List`1), kept as a part of the name of a method.
    private static readonly Regex _word = new(@"`[0-9]{1,2}\b|[A-Za-z0-9_]+", _options);

    // After a key: the frame of a method, CancellationToken:ThrowIfCancellationRequested (), not a value.
    private static readonly Regex _frameAfterKey = new(@"\A\w+\s*\(", _options);

    public static string Scrub(string text)
    {
        text = _url.Replace(text, "<url>");
        text = _urlWithoutHost.Replace(text, "<url>");
        text = _jwt.Replace(text, "<secret>");
        text = _authScheme.Replace(text, "$1 <secret>");
        text = _secretValue.Replace(text, ScrubSecretValue);
        text = _pathWithQuery.Replace(text, "<url>");
        text = _queryAfterDelimiter.Replace(text, "<url>");
        text = _queryOfPairs.Replace(text, "<url>");
        text = _personalValue.Replace(text, ScrubPersonalValue);
        text = _email.Replace(text, "<email>");
        text = _guid.Replace(text, "<guid>");
        text = _ipV4.Replace(text, "<ip>");
        text = _ipV6.Replace(text, ScrubIpV6);
        text = _percentEncoded.Replace(text, "<text>");
        text = _base64.Replace(text, "<id>$1");
        text = _doubleQuoted.Replace(text, "\"<str>\"");
        text = _singleQuoted.Replace(text, "$1'<str>'");
        text = _backticked.Replace(text, "$1`<str>`");
        text = _braced.Replace(text, "{<str>}");
        text = _typographicQuoted.Replace(text, "<str>");
        text = _notPrintableAscii.Replace(text, "<text>");
        text = _angledWithSpace.Replace(text, "<str>");
        text = _wwwHost.Replace(text, "<url>");
        text = _absolutePath.Replace(text, "$1<path>");
        text = _relativePath.Replace(text, ScrubRelativePath);
        text = _keyValue.Replace(text, "$1=<v>");
        text = _hostWithPort.Replace(text, "<url>");
        text = _domain.Replace(text, "<url>");
        text = _upperDomain.Replace(text, "<url>");
        text = _fileName.Replace(text, "<file>");
        text = _word.Replace(text, ScrubWord);
        return text;
    }

    private static string ScrubSecretValue(Match match)
    {
        return _frameAfterKey.IsMatch(match.Groups[2].Value) ? match.Value : match.Groups[1].Value + "=<secret>";
    }

    private static string ScrubPersonalValue(Match match)
    {
        return _frameAfterKey.IsMatch(match.Groups[2].Value) ? match.Value : match.Groups[1].Value + "=<text>";
    }

    private static string ScrubIpV6(Match match)
    {
        return match.Groups[2].Value.Any(char.IsAsciiDigit) ? match.Groups[1].Value + "<ip>" : match.Value;
    }

    private static string ScrubRelativePath(Match match)
    {
        return _nestedTypeFrame.IsMatch(match.Value) ? match.Value : "<path>";
    }

    // Names of code keep their short digits (Vector3, Int32, d__12, DisplayClass12_0), anything else with digits is an id.
    private static string ScrubWord(Match match)
    {
        var word = match.Value;

        if (word[0] == '`')
            return word;

        if (word.All(char.IsAsciiDigit))
            return "<n>";

        if (word.Length >= _minHexWordLength && word.All(char.IsAsciiHexDigit))
            return "<id>";

        if (!word.Any(char.IsAsciiDigit))
            return word.Length >= _maxPlainWordLength ? "<id>" : word;

        return IsCodeNameWithDigits(word) ? word : "<id>";
    }

    private static bool IsCodeNameWithDigits(string word)
    {
        if (char.IsAsciiDigit(word[0]) || word.Length >= _maxWordWithDigitsLength)
            return false;

        if (word.Length >= _maxUpperWordWithDigitsLength && !word.Any(char.IsAsciiLetterLower))
            return false;

        var digitRuns = 0;
        var runLength = 0;

        foreach (var symbol in word)
        {
            if (!char.IsAsciiDigit(symbol))
            {
                runLength = 0;
                continue;
            }

            if (runLength == 0)
                digitRuns++;

            runLength++;
            if (runLength > _maxShortDigitRun)
                return false;
        }

        // A1B2C3 and x2y4z6 are generated, not named.
        return digitRuns <= 2 && (digitRuns == 1 || word.Any(char.IsAsciiLetterLower));
    }
}
