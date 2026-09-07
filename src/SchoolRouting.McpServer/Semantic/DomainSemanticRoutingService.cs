using System.Text;
using System.Text.RegularExpressions;
using SchoolRouting.Contracts;
using SchoolRouting.McpServer.Data;

namespace SchoolRouting.McpServer.Semantic;

/// <summary>
/// Offline semantic-search baseline for the demo. Domain aliases are projected to
/// shared concepts and combined with direct keyword and character n-gram scores.
/// The service boundary can later be replaced by an AI embedding implementation.
/// </summary>
public sealed partial class DomainSemanticRoutingService(ISchoolRepository repository)
    : ISemanticRoutingService
{
    private static readonly SemanticConcept[] Concepts =
    [
        new("學籍註冊", ["註冊", "學籍", "休學", "退學", "畢業資格", "在學證明"]),
        new("課務成績", ["排課", "選課", "課程", "教室", "成績", "教學"]),
        new("招生入學", ["招生", "甄選", "錄取", "新生", "入學"]),
        new("學生輔導", ["學生獎懲", "學生請假", "生活輔導", "學生緊急事件"]),
        new("學生住宿", ["宿舍", "住宿", "退宿", "床位", "舍寢"]),
        new("學生社團", ["社團", "學生組織", "課外活動", "迎新"]),
        new("營繕修繕", ["修繕", "漏水", "施工", "校舍", "營繕", "公共設施", "場域改善"]),
        new("採購契約", ["採購", "招標", "比價", "驗收", "採購法", "財物契約"]),
        new("財產事務", ["財產", "盤點", "場地借用", "清潔", "保全", "停車", "車輛"]),
        new("研究計畫", ["研究計畫", "國科會", "科技部", "研究成果", "論文", "結案"]),
        new("產學智財", ["產學合作", "技術移轉", "企業媒合", "專利", "智慧財產"]),
        new("研究倫理", ["研究倫理", "學術倫理", "研究誠信", "抄襲", "學術不端"]),
        new("人員任用", ["聘任", "新聘", "升等", "離職", "人事資料", "服務證明"]),
        new("差勤管理", ["差勤", "出勤", "請假", "加班", "刷卡", "出勤異常"]),
        new("教職員訓練", ["教職員訓練", "教育訓練", "研習", "訓練時數", "人才發展"]),
        new("預算管理", ["預算", "預算流用", "追加預算", "預算執行"]),
        new("會計核銷", ["核銷", "會計", "憑證", "決算", "財務報表"]),
        new("出納收付", ["出納", "匯款", "薪資發放", "付款", "學雜費", "繳費", "入帳"]),
        new("資訊安全", ["資訊安全", "資安", "勒索軟體", "惡意程式", "資料外洩", "個資外洩", "駭客", "網路攻擊", "入侵", "弱點", "釣魚郵件", "社交工程"]),
        new("資訊系統", ["資訊系統", "校務系統", "系統開發", "程式開發", "資料介接", "API", "帳號", "權限", "單一登入", "SSO"]),
        new("圖書資源", ["圖書", "館藏", "編目", "電子期刊", "學術資料庫", "借閱", "讀者服務"]),
        new("國際交流", ["國際合作", "姐妹校", "外賓", "國際交流", "海外交流", "國際活動"]),
        new("境外學生", ["境外生", "外籍生", "居留", "居留證", "簽證", "工作許可"]),
        new("交換學生", ["交換學生", "海外交換", "交換計畫", "薦送", "交換學分"]),
        new("公文檔案", ["公文", "收文", "發文", "分文", "歸檔", "文書", "檔案管理"]),
        new("校級會議", ["校務會議", "行政會議", "會議提案", "會議決議", "列管", "議程"]),
        new("媒體公關", ["新聞", "媒體", "公關", "校譽", "記者會", "形象宣傳", "採訪"]),
        new("職業安全", ["職業安全", "職安", "職災", "作業安全", "職業災害"]),
        new("實驗室安全", ["實驗室", "化學品", "危害性化學品", "安全檢查", "缺失改善"]),
        new("環境永續", ["環境管理", "廢棄物", "節能", "永續", "溫室氣體", "碳排"]),
        new("消防防災", ["消防", "逃生", "災害防救", "防災", "火災"])
    ];

    public async Task<IReadOnlyList<ResponsibilityCandidate>> SearchAsync(
        string documentText,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentText))
        {
            return [];
        }

        var sourceCandidates = await repository.ListResponsibilityCandidatesAsync(cancellationToken);
        var documentConcepts = FindConcepts(documentText);
        var documentNgrams = CreateNgrams(documentText);
        var ranked = new List<ResponsibilityCandidate>(sourceCandidates.Count);

        foreach (var candidate in sourceCandidates)
        {
            var candidateText = string.Join(' ',
                candidate.DepartmentName,
                candidate.PersonTitle,
                candidate.Description,
                candidate.Keywords);
            var candidateConcepts = FindConcepts(candidateText);
            var sharedConcepts = documentConcepts
                .Intersect(candidateConcepts)
                .OrderBy(value => value)
                .ToArray();

            var conceptRecall = documentConcepts.Count == 0
                ? 0d
                : (double)sharedConcepts.Length / documentConcepts.Count;
            var ngramSimilarity = DiceCoefficient(documentNgrams, CreateNgrams(candidateText));
            var semanticSimilarity = documentConcepts.Count > 0
                ? (conceptRecall * 0.9) + (ngramSimilarity * 0.1)
                : ngramSimilarity;

            var directTerms = SplitKeywords(candidate.Keywords)
                .Where(term => documentText.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var keywordRatio = Math.Min(directTerms.Length / 3d, 1d);
            var priorityRatio = candidate.Priority / 5d;
            var finalScore = (semanticSimilarity * 0.75)
                + (keywordRatio * 0.20)
                + (priorityRatio * 0.05);

            var semanticScore = ToPercentage(semanticSimilarity);
            var keywordScore = ToPercentage(keywordRatio);
            var matchScore = ToPercentage(finalScore);
            var confidence = matchScore >= 70 ? "高"
                : matchScore >= 45 ? "中"
                : "低－需人工判斷";
            var matchedTerms = sharedConcepts.Concat(directTerms)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var reason = matchedTerms.Length > 0
                ? $"相關概念：{string.Join("、", matchedTerms)}"
                : "沒有明確的領域概念，僅依文字相似度提供候選。";

            ranked.Add(candidate with
            {
                MatchedTerms = matchedTerms,
                MatchScore = matchScore,
                SemanticScore = semanticScore,
                KeywordScore = keywordScore,
                Confidence = confidence,
                MatchReason = reason
            });
        }

        return ranked
            .OrderByDescending(candidate => candidate.MatchScore)
            .ThenByDescending(candidate => candidate.SemanticScore)
            .ThenByDescending(candidate => candidate.Priority)
            .Take(Math.Clamp(limit, 1, 30))
            .ToArray();
    }

    private static HashSet<string> FindConcepts(string text) => Concepts
        .Where(concept => concept.Aliases.Any(alias =>
            text.Contains(alias, StringComparison.OrdinalIgnoreCase)))
        .Select(concept => concept.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string[] SplitKeywords(string keywords) => KeywordSeparatorRegex()
        .Split(keywords)
        .Where(term => term.Length >= 2)
        .ToArray();

    private static HashSet<string> CreateNgrams(string text)
    {
        var normalized = NormalizeRegex().Replace(text.ToLowerInvariant(), string.Empty);
        var result = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < normalized.Length - 1; index++)
        {
            result.Add(normalized.Substring(index, 2));
        }

        return result;
    }

    private static double DiceCoefficient(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var intersection = left.Count <= right.Count
            ? left.Count(right.Contains)
            : right.Count(left.Contains);
        return (2d * intersection) / (left.Count + right.Count);
    }

    private static int ToPercentage(double value) =>
        (int)Math.Round(Math.Clamp(value, 0d, 1d) * 100d);

    private sealed record SemanticConcept(string Name, string[] Aliases);

    [GeneratedRegex(@"[,，、；;\s]+")]
    private static partial Regex KeywordSeparatorRegex();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NormalizeRegex();
}
