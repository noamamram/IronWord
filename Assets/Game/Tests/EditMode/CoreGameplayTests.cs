using System.Collections.Generic;
using NUnit.Framework;
using TypingDefense.Application.Spawning;
using TypingDefense.Application.Targeting;
using TypingDefense.Domain.Common;
using TypingDefense.Domain.Languages;
using TypingDefense.Domain.Scoring;
using TypingDefense.Domain.Typing;
using TypingDefense.Simulation.Trajectories;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TypingDefense.Tests.EditMode
{
    public sealed class EnglishLanguageRulesTests
    {
        [Test]
        public void Normalize_MakesLowercase()
        {
            var rules = new EnglishLanguageRules(new[] { "a", "b", "c" });
            Assert.AreEqual("a", rules.Normalize("A"));
        }

        [Test]
        public void AreEqual_IgnoresCase()
        {
            var rules = new EnglishLanguageRules(new[] { "a" });
            Assert.IsTrue(rules.AreEqual("A", "a"));
        }

        [Test]
        public void IsAllowed_RejectsUnknown()
        {
            var rules = new EnglishLanguageRules(new[] { "a", "b" });
            Assert.IsFalse(rules.IsAllowed("z"));
        }
    }

    public sealed class HebrewLanguageRulesTests
    {
        [Test]
        public void Normalize_MapsFinalLetters()
        {
            var rules = new HebrewLanguageRules(new[] { "כ", "מ" }, mapFinalLetters: true);
            Assert.AreEqual("כ", rules.Normalize("ך"));
            Assert.AreEqual("מ", rules.Normalize("ם"));
        }

        [Test]
        public void Normalize_StripsDiacritics()
        {
            var rules = new HebrewLanguageRules(new[] { "ש" }, stripDiacritics: true);
            Assert.AreEqual("ש", rules.Normalize("שׁ"));
        }
    }

    public sealed class SingleElementChallengeTests
    {
        [Test]
        public void Submit_CorrectLetter_Completes()
        {
            var rules = new EnglishLanguageRules(new[] { "a" });
            var challenge = new SingleElementChallenge("a", rules);
            var result = challenge.Submit("A");
            Assert.AreEqual(ChallengeInputOutcome.Completed, result.Outcome);
            Assert.IsTrue(challenge.IsCompleted);
        }

        [Test]
        public void Submit_IncorrectLetter_ReportsIncorrect()
        {
            var rules = new EnglishLanguageRules(new[] { "a", "b" });
            var challenge = new SingleElementChallenge("a", rules);
            var result = challenge.Submit("b");
            Assert.AreEqual(ChallengeInputOutcome.Incorrect, result.Outcome);
            Assert.IsFalse(challenge.IsCompleted);
        }
    }

    public sealed class TokenAllocatorTests
    {
        [Test]
        public void TryAllocateLetter_AvoidsDuplicatesWhileLeased()
        {
            var repo = new InMemoryTokenRepository(new[]
            {
                new TokenEntry("a", "a", tags: new[] { "Letter" }),
                new TokenEntry("b", "b", tags: new[] { "Letter" })
            });
            var rules = new EnglishLanguageRules(new[] { "a", "b" });
            var allocator = new TokenAllocator(repo, rules, seed: 7);

            Assert.IsTrue(allocator.TryAllocateLetter(out var first));
            Assert.IsTrue(allocator.TryAllocateLetter(out var second));
            Assert.AreNotEqual(rules.Normalize(first.Text), rules.Normalize(second.Text));
        }

        [Test]
        public void TryAllocateLetter_UsesFullBagBeforeHeavyRepeats()
        {
            var tokens = new TokenEntry[5];
            for (var i = 0; i < 5; i++)
            {
                var letter = ((char)('a' + i)).ToString();
                tokens[i] = new TokenEntry(letter, letter, tags: new[] { "Letter" });
            }

            var repo = new InMemoryTokenRepository(tokens);
            var rules = new EnglishLanguageRules(new[] { "a", "b", "c", "d", "e" });
            var allocator = new TokenAllocator(repo, rules, seed: 99);

            var seen = new HashSet<string>();
            for (var i = 0; i < 5; i++)
            {
                Assert.IsTrue(allocator.TryAllocateLetter(out var token));
                seen.Add(rules.Normalize(token.Text));
                allocator.Release(token.Text);
            }

            Assert.AreEqual(5, seen.Count);
        }

        [Test]
        public void ReshuffleForNewRun_ChangesDrawOrderWithDifferentSeeds()
        {
            var tokens = new TokenEntry[8];
            for (var i = 0; i < 8; i++)
            {
                var letter = ((char)('a' + i)).ToString();
                tokens[i] = new TokenEntry(letter, letter, tags: new[] { "Letter" });
            }

            var repo = new InMemoryTokenRepository(tokens);
            var rules = new EnglishLanguageRules(new[] { "a", "b", "c", "d", "e", "f", "g", "h" });
            var allocator = new TokenAllocator(repo, rules, seed: 1);

            string Sequence()
            {
                var parts = new List<string>(8);
                for (var i = 0; i < 8; i++)
                {
                    Assert.IsTrue(allocator.TryAllocateLetter(out var token));
                    parts.Add(rules.Normalize(token.Text));
                    allocator.Release(token.Text);
                }

                return string.Concat(parts);
            }

            var first = Sequence();
            allocator.ReshuffleForNewRun(4242);
            var second = Sequence();
            allocator.ReshuffleForNewRun(7777);
            var third = Sequence();

            Assert.AreNotEqual(first, second);
            Assert.AreNotEqual(second, third);
        }

        [Test]
        public void TryAllocateSequence_LeasesDistinctLetters()
        {
            var tokens = new TokenEntry[6];
            for (var i = 0; i < 6; i++)
            {
                var letter = ((char)('a' + i)).ToString();
                tokens[i] = new TokenEntry(letter, letter, tags: new[] { "Letter" });
            }

            var repo = new InMemoryTokenRepository(tokens);
            var rules = new EnglishLanguageRules(new[] { "a", "b", "c", "d", "e", "f" });
            var allocator = new TokenAllocator(repo, rules, seed: 11);

            Assert.IsTrue(allocator.TryAllocateSequence(3, out var sequence));
            Assert.AreEqual(3, sequence.Count);
            Assert.AreNotEqual(sequence[0], sequence[1]);
            Assert.AreNotEqual(sequence[1], sequence[2]);
        }
    }

    public sealed class SequenceChallengeTests
    {
        [Test]
        public void Submit_AcceptsLettersInAnyOrder()
        {
            var rules = new EnglishLanguageRules(new[] { "a", "b", "c" });
            var challenge = new SequenceChallenge(new[] { "a", "b", "c" }, rules);

            Assert.AreEqual(ChallengeInputOutcome.CorrectProgress, challenge.Submit("c").Outcome);
            Assert.AreEqual("ab", challenge.RemainingText);
            Assert.AreEqual(ChallengeInputOutcome.CorrectProgress, challenge.Submit("a").Outcome);
            Assert.AreEqual("b", challenge.RemainingText);
            Assert.AreEqual(ChallengeInputOutcome.Completed, challenge.Submit("b").Outcome);
            Assert.IsTrue(challenge.IsCompleted);
        }

        [Test]
        public void Submit_RejectsLetterNotInRemaining()
        {
            var rules = new EnglishLanguageRules(new[] { "a", "b", "c" });
            var challenge = new SequenceChallenge(new[] { "a", "b", "c" }, rules);
            challenge.Submit("a");
            Assert.AreEqual(ChallengeInputOutcome.Incorrect, challenge.Submit("a").Outcome);
            Assert.AreEqual(1, challenge.Progress);
            Assert.AreEqual("bc", challenge.RemainingText);
        }

        [Test]
        public void Accepts_MatchesAnyRemainingLetter()
        {
            var rules = new EnglishLanguageRules(new[] { "a", "b", "c" });
            var challenge = new SequenceChallenge(new[] { "a", "b", "c" }, rules);
            Assert.IsTrue(challenge.Accepts("b"));
            challenge.Submit("b");
            Assert.IsFalse(challenge.Accepts("b"));
            Assert.IsTrue(challenge.Accepts("a"));
        }
    }

    public sealed class ScoreServiceTests
    {
        [Test]
        public void AwardInterception_IncreasesScoreAndCombo()
        {
            var score = new ScoreService(new ScoringProfileData { BaseThreatScore = 100 });
            var result = score.AwardInterception(1, 1f);
            Assert.Greater(result.PointsAwarded, 0);
            Assert.AreEqual(1, result.Combo);
            Assert.AreEqual(result.PointsAwarded, result.TotalScore);
        }

        [Test]
        public void RegisterMistake_ResetsCombo()
        {
            var score = new ScoreService(new ScoringProfileData());
            score.AwardInterception(1, 1f);
            score.RegisterMistake();
            Assert.AreEqual(0, score.Combo);
        }
    }

    public sealed class ArcTrajectoryTests
    {
        [Test]
        public void EvaluatePosition_StartsAndEndsAtEndpoints()
        {
            var start = new Vector3(0f, 10f, 0f);
            var end = new Vector3(0f, 0f, 100f);
            var trajectory = new ArcTrajectory(start, end, Vector3.up, 20f);

            Assert.AreEqual(start.x, trajectory.EvaluatePosition(0f).x, 0.001f);
            Assert.AreEqual(start.y, trajectory.EvaluatePosition(0f).y, 0.001f);
            Assert.AreEqual(start.z, trajectory.EvaluatePosition(0f).z, 0.001f);

            Assert.AreEqual(end.x, trajectory.EvaluatePosition(1f).x, 0.001f);
            Assert.AreEqual(end.y, trajectory.EvaluatePosition(1f).y, 0.001f);
            Assert.AreEqual(end.z, trajectory.EvaluatePosition(1f).z, 0.001f);
        }

        [Test]
        public void EvaluatePosition_PeaksNearMidpoint()
        {
            var start = new Vector3(0f, 0f, 0f);
            var end = new Vector3(0f, 0f, 100f);
            var height = 25f;
            var trajectory = new ArcTrajectory(start, end, Vector3.up, height);
            var mid = trajectory.EvaluatePosition(0.5f);
            Assert.AreEqual(height, mid.y, 0.001f);
        }
    }

    public sealed class TargetSelectorTests
    {
        [Test]
        public void Select_PrefersSoonestImpact()
        {
            var rules = new EnglishLanguageRules(new[] { "a" });
            var selector = new PriorityTypingTargetSelector();
            var challengeA = new SingleElementChallenge("a", rules);
            var challengeB = new SingleElementChallenge("a", rules);

            var far = new TypingTarget(new ThreatId(1), challengeA, 10f, 0f, 50f);
            var near = new TypingTarget(new ThreatId(2), challengeB, 3f, 1f, 80f);

            var selected = selector.Select("a", null, new[] { far, near });
            Assert.AreEqual(2, selected.Id.Value);
        }
    }

    public sealed class CameraRelativeSpawnTests
    {
        [Test]
        public void SkySpawn_SpreadsAcrossCameraView()
        {
            var camGo = new GameObject("TestCam");
            camGo.transform.position = new Vector3(0f, 8f, -18f);
            camGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 100f;

            var provider = new CameraRelativeSkySpawnProvider(
                () => camGo.transform,
                minDistance: 120f,
                maxDistance: 230f,
                minHeight: 40f,
                maxHeight: 80f,
                horizontalFovFraction: 0.85f,
                seed: 42);

            var left = 0;
            var right = 0;
            for (var i = 0; i < 80; i++)
            {
                Assert.IsTrue(provider.TryGetSpawnPoint(out var p));
                Assert.Greater(p.z, camGo.transform.position.z);
                Assert.GreaterOrEqual(p.y, 40f);
                Assert.LessOrEqual(p.y, 80f);

                var toPoint = p - camGo.transform.position;
                var flat = Vector3.ProjectOnPlane(toPoint, Vector3.up);
                var forward = Vector3.ProjectOnPlane(camGo.transform.forward, Vector3.up).normalized;
                var signed = Vector3.SignedAngle(forward, flat.normalized, Vector3.up);
                if (signed < -2f) left++;
                else if (signed > 2f) right++;
            }

            Object.DestroyImmediate(camGo);
            Assert.Greater(left, 10);
            Assert.Greater(right, 10);
        }
    }
}
