namespace NN.FeatureFlags.Tests;

public sealed class FeatureFlagEvaluatorTests
{
    private static readonly string[] AcmeTenant = { "acme" };
    private static readonly string[] User42 = { "user-42" };

    [Fact]
    public void UnknownKeyIsDisabled()
    {
        var evaluator = CreateEvaluator();

        Assert.False(evaluator.IsEnabled("missing"));
        Assert.False(evaluator.IsEnabled("missing", new FeatureFlagContext(UserId: "user-1")));
    }

    [Fact]
    public void UnknownKeyUsesSuppliedDefault()
    {
        var evaluator = CreateEvaluator();

        Assert.True(evaluator.IsEnabled("missing", defaultValue: true));
        Assert.True(evaluator.IsEnabled("missing", defaultValue: true, new FeatureFlagContext(UserId: "user-1")));
        Assert.False(evaluator.IsEnabled("missing", defaultValue: false));
    }

    [Fact]
    public void BlankKeyIsAlwaysDisabled()
    {
        var evaluator = CreateEvaluator();

        Assert.False(evaluator.IsEnabled(" "));
        Assert.False(evaluator.IsEnabled(string.Empty, defaultValue: true));
    }

    [Fact]
    public void DisabledFlagIsDisabled()
    {
        var evaluator = CreateEvaluator(new FeatureFlagDefinition("flag", IsEnabled: false));

        Assert.False(evaluator.IsEnabled("flag"));
        Assert.False(evaluator.IsEnabled("flag", new FeatureFlagContext(UserId: "user-1")));
    }

    [Fact]
    public void FlagWithoutTargetingIsEnabledForEveryone()
    {
        var evaluator = CreateEvaluator(new FeatureFlagDefinition("flag", IsEnabled: true));

        Assert.True(evaluator.IsEnabled("flag"));
        Assert.True(evaluator.IsEnabled("flag", new FeatureFlagContext(TenantId: "acme")));
    }

    [Fact]
    public void AllowListsAreHonored()
    {
        var evaluator = CreateEvaluator(
            new FeatureFlagDefinition(
                "flag",
                IsEnabled: true,
                new TargetingPolicy(
                    TenantAllowList: AcmeTenant,
                    UserAllowList: User42)));

        Assert.True(evaluator.IsEnabled("flag", new FeatureFlagContext(TenantId: "acme")));
        Assert.True(evaluator.IsEnabled("flag", new FeatureFlagContext(UserId: "user-42")));
        Assert.False(evaluator.IsEnabled("flag", new FeatureFlagContext(TenantId: "other", UserId: "someone")));
        Assert.False(evaluator.IsEnabled("flag"));
    }

    [Fact]
    public void PartialRolloutIsDeterministic()
    {
        var definition = new FeatureFlagDefinition(
            "flag",
            IsEnabled: true,
            TargetingPolicy.Percentage(50));

        var first = CreateEvaluator(definition);
        var second = CreateEvaluator(definition);

        Assert.True(first.IsEnabled("flag", new FeatureFlagContext(UserId: "user-1")));
        Assert.False(first.IsEnabled("flag", new FeatureFlagContext(UserId: "user-3")));

        for (var index = 1; index <= 30; index++)
        {
            var userId = $"user-{index}";
            var expected = first.IsEnabled("flag", new FeatureFlagContext(UserId: userId));

            Assert.Equal(expected, first.IsEnabled("flag", new FeatureFlagContext(UserId: userId)));
            Assert.Equal(expected, second.IsEnabled("flag", new FeatureFlagContext(UserId: userId)));
        }
    }

    [Fact]
    public void TargetKeyOverridesUserIdForBucketing()
    {
        var evaluator = CreateEvaluator(
            new FeatureFlagDefinition(
                "flag",
                IsEnabled: true,
                TargetingPolicy.Percentage(50)));

        Assert.True(evaluator.IsEnabled(
            "flag",
            new FeatureFlagContext(UserId: "user-3") { TargetingKey = "user-1" }));

        Assert.False(evaluator.IsEnabled(
            "flag",
            new FeatureFlagContext(UserId: "user-1") { TargetingKey = "user-3" }));
    }

    [Fact]
    public void TenantOnlyContextCannotParticipateInPartialRollout()
    {
        var evaluator = CreateEvaluator(
            new FeatureFlagDefinition(
                "flag",
                IsEnabled: true,
                TargetingPolicy.Percentage(50)));

        Assert.False(evaluator.IsEnabled("flag", new FeatureFlagContext(TenantId: "acme")));
    }

    [Fact]
    public void RolloutBoundariesAreInclusive()
    {
        var off = CreateEvaluator(
            new FeatureFlagDefinition("flag", IsEnabled: true, TargetingPolicy.Percentage(0)));
        var on = CreateEvaluator(
            new FeatureFlagDefinition("flag", IsEnabled: true, TargetingPolicy.Percentage(100)));

        Assert.False(off.IsEnabled("flag", new FeatureFlagContext(UserId: "user-1")));
        Assert.True(on.IsEnabled("flag"));
        Assert.True(on.IsEnabled("flag", new FeatureFlagContext(TenantId: "acme")));
    }

    [Fact]
    public void ContextCopiesAttributes()
    {
        var source = new Dictionary<string, string>(StringComparer.Ordinal) { ["region"] = "eu" };
        var context = new FeatureFlagContext { Attributes = source };

        source["region"] = "us";

        Assert.Equal("eu", context.Attributes["region"]);
    }

    [Fact]
    public void ContextAttributesDefaultToEmpty()
    {
        var context = new FeatureFlagContext();

        Assert.Empty(context.Attributes);
        Assert.Null(context.TargetingKey);
    }

    private static FeatureFlagEvaluator CreateEvaluator(params FeatureFlagDefinition[] flags)
    {
        var map = new Dictionary<string, FeatureFlagDefinition>(StringComparer.Ordinal);

        foreach (var flag in flags)
        {
            map.Add(flag.Key, flag);
        }

        return new FeatureFlagEvaluator(new FeatureFlagSnapshot("v1", map));
    }
}
