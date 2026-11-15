using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// Test Runner 실행이 끝날 때마다 결과를 마크다운으로 저장한다.
    /// - production/qa/test-runs/last-test-run.md : 항상 최신 결과로 덮어쓴다(Claude가 읽는 파일).
    /// - production/qa/test-runs/yyyyMMdd-HHmmss.md : 실행 기록 보관용.
    /// 통과한 테스트도 TestContext.WriteLine 출력(측정값)을 남긴다.
    /// </summary>
    [InitializeOnLoad]
    static class TestRunReporter
    {
        const string OutputDir = "production/qa/test-runs";
        const string LatestFile = "last-test-run.md";

        static TestRunReporter()
        {
            // 도메인이 다시 로드될 때마다(컴파일, Play 진입) 정적 생성자가 다시 돌므로 매번 새로 등록한다.
            TestRunnerApi.RegisterTestCallback(new Callbacks());
        }

        class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    Write(result);
                }
                catch (Exception e)
                {
                    // 리포터 오류가 테스트 결과 표시를 막으면 안 된다.
                    Debug.LogWarning($"[TestRunReporter] 결과 저장 실패: {e.Message}");
                }
            }
        }

        static void Write(ITestResultAdaptor root)
        {
            var leaves = new List<ITestResultAdaptor>();
            CollectLeaves(root, leaves);
            if (leaves.Count == 0) return;

            int passed = leaves.Count(r => r.TestStatus == TestStatus.Passed);
            int failed = leaves.Count(r => r.TestStatus == TestStatus.Failed);
            int skipped = leaves.Count(r => r.TestStatus == TestStatus.Skipped);
            int inconclusive = leaves.Count(r => r.TestStatus == TestStatus.Inconclusive);
            DateTime now = DateTime.Now;

            var sb = new StringBuilder();
            sb.AppendLine("# 테스트 실행 결과");
            sb.AppendLine();
            sb.AppendLine($"- **실행 시각**: {now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- **대상**: `{root.FullName}`");
            sb.AppendLine($"- **결과**: 전체 {leaves.Count} / 통과 {passed} / 실패 {failed} / 건너뜀 {skipped} / 판정 불가 {inconclusive}");
            sb.AppendLine($"- **소요 시간**: {root.Duration:F1}초");
            sb.AppendLine();

            sb.AppendLine("## 요약");
            sb.AppendLine();
            sb.AppendLine("| 결과 | 테스트 | 시간(초) |");
            sb.AppendLine("|---|---|---|");
            foreach (ITestResultAdaptor r in leaves)
                sb.AppendLine($"| {StatusLabel(r.TestStatus)} | `{r.Name}` | {r.Duration:F1} |");
            sb.AppendLine();

            sb.AppendLine("## 상세");
            foreach (ITestResultAdaptor r in leaves.OrderBy(r => r.TestStatus == TestStatus.Failed ? 0 : 1))
            {
                sb.AppendLine();
                sb.AppendLine($"### {StatusLabel(r.TestStatus)} `{r.Name}`");
                sb.AppendLine($"- 전체 이름: `{r.FullName}`");

                if (!string.IsNullOrWhiteSpace(r.Message))
                {
                    sb.AppendLine("- 메시지:");
                    sb.AppendLine("```");
                    sb.AppendLine(r.Message.Trim());
                    sb.AppendLine("```");
                }
                if (!string.IsNullOrWhiteSpace(r.Output))
                {
                    sb.AppendLine("- 출력(측정값):");
                    sb.AppendLine("```");
                    sb.AppendLine(FilterOutput(r.Output));
                    sb.AppendLine("```");
                }
                if (r.TestStatus == TestStatus.Failed && !string.IsNullOrWhiteSpace(r.StackTrace))
                {
                    sb.AppendLine("- 스택(프로젝트 코드만):");
                    sb.AppendLine("```");
                    sb.AppendLine(ProjectFrames(r.StackTrace));
                    sb.AppendLine("```");
                }
            }

            string dir = Path.Combine(Directory.GetCurrentDirectory(), OutputDir);
            Directory.CreateDirectory(dir);
            string text = sb.ToString();
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(dir, LatestFile), text, utf8);
            File.WriteAllText(Path.Combine(dir, $"{now:yyyyMMdd-HHmmss}.md"), text, utf8);

            Debug.Log($"[TestRunReporter] 테스트 결과를 저장했다: {OutputDir}/{LatestFile} (통과 {passed}, 실패 {failed})");
        }

        static void CollectLeaves(ITestResultAdaptor result, List<ITestResultAdaptor> leaves)
        {
            if (!result.HasChildren)
            {
                leaves.Add(result);
                return;
            }
            foreach (ITestResultAdaptor child in result.Children) CollectLeaves(child, leaves);
        }

        static string StatusLabel(TestStatus status)
        {
            switch (status)
            {
                case TestStatus.Passed: return "✅ 통과";
                case TestStatus.Failed: return "❌ 실패";
                case TestStatus.Skipped: return "⏭ 건너뜀";
                default: return "❔ 판정 불가";
            }
        }

        /// <summary>테스트 출력에서 "Hit: n" 같은 반복 로그와 스택 줄을 걸러 측정값만 남긴다.</summary>
        static string FilterOutput(string output)
        {
            IEnumerable<string> lines = output.Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => l.Length > 0)
                .Where(l => !l.StartsWith("Hit: "))
                .Where(l => !l.TrimStart().StartsWith("at ") && !l.Contains("(at "));
            string joined = string.Join("\n", lines);
            return joined.Length > 0 ? joined : "(측정값 없음)";
        }

        static string ProjectFrames(string stackTrace)
        {
            string[] frames = stackTrace.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Contains("Assets/") || l.Contains("Assets\\"))
                .Take(8)
                .ToArray();
            return frames.Length > 0 ? string.Join("\n", frames) : stackTrace.Trim();
        }
    }
}
