using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Content.Client.Guidebook.Components;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Guidebook;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Onyx.Xenobiology;

public sealed class XenobiologyAssetsGuideTest : GameTest
{
    private static readonly Regex RsiPathRegex = new(@"(?:sprite:\s*|icon:\s*\{\s*sprite:\s*)(?<path>[^\s,}]+\.rsi)", RegexOptions.Compiled);
    private static readonly Regex PngPathRegex = new(@"(?:sprite:\s*|icon:\s*)(?<path>[^\s,}]+\.png)", RegexOptions.Compiled);
    private static readonly Regex AudioPathRegex = new(@"(?<path>/Audio/[^\s,}\]]+\.(?:ogg|wav))", RegexOptions.Compiled);
    private static readonly Regex ShaderPathRegex = new("path:\\s*[\\\"']?(?<path>/Textures/[^\\s\\\"']+\\.swsl)", RegexOptions.Compiled);
    private static readonly Regex LocaleKeyRegex = new(@"(?m)^(?<key>[a-zA-Z0-9][a-zA-Z0-9_-]*)\s*=", RegexOptions.Compiled);
    private static readonly ProtoId<GuideEntryPrototype> XenobiologyGuide = "XenobiologyGuide";
    private static readonly ProtoId<GuideEntryPrototype> ScienceGuide = "Science";

    private static readonly Dictionary<string, string[]> RequiredRsiStates = new()
    {
        ["/Textures/_Onyx/Xenobiology/Actions/actions_slime.rsi"] = ["slimeeat", "slimesplit"],
        ["/Textures/_Onyx/Xenobiology/Mobs/slimesBaby.rsi"] = ["base", "dead"],
        ["/Textures/_Onyx/Xenobiology/Mobs/slimesAdult.rsi"] = ["base", "dead"],
        ["/Textures/_Onyx/Xenobiology/Extracts/extract.rsi"] = ["core", "inhand-left", "inhand-right"],
        ["/Textures/_Onyx/Xenobiology/Items/extract_jellies.rsi"] = ["jelly", "inhand-left", "inhand-right"],
        ["/Textures/_Onyx/Xenobiology/Floors/floors.rsi"] = ["bluespace", "sepia", "icon-bluespace", "icon-sepia"],
        ["/Textures/_Onyx/Xenobiology/Machines/slime_processor.rsi"] = ["processor", "processor_on", "processor_open"],
        ["/Textures/_Onyx/Xenobiology/Equipment/Scanner/slime_scanner.rsi"] = ["icon", "analyzer", "analyzer-inhand-left", "analyzer-inhand-right"],
        ["/Textures/_Onyx/Xenobiology/Equipment/Xenovac/slime_nozzle.rsi"] = ["icon", "inhand-left", "inhand-right"],
        ["/Textures/_Onyx/Xenobiology/Equipment/Xenovac/slime_pack.rsi"] = ["icon", "icon-filled", "inhand-left", "inhand-right", "equipped-SUITSTORAGE"],
        ["/Textures/_Onyx/Xenobiology/Items/volatile_organ.rsi"] = ["icon", "inhand-left", "inhand-right"],
        ["/Textures/_Onyx/Xenobiology/Items/goo_ball.rsi"] = ["icon", "goo-wall", "inhand-left", "inhand-right"],
        ["/Textures/_Onyx/Xenobiology/Materials/adamantine_bar.rsi"] = ["icon"],
        ["/Textures/_Onyx/Xenobiology/Clothing/Belt/xenobag.rsi"] = ["icon", "equipped-BELT"],
        ["/Textures/_Onyx/Xenobiology/Clothing/Belt/xenobag_holding.rsi"] = ["icon", "equipped-BELT", "inhand-left", "inhand-right"],
    };

    private static readonly string[] PrototypeDirectories =
    [
        "/Prototypes/_Onyx/Actions/", "/Prototypes/_Onyx/Catalog/", "/Prototypes/_Onyx/Entities/",
        "/Prototypes/_Onyx/HTN/", "/Prototypes/_Onyx/Reagents/Xenobiology/",
        "/Prototypes/_Onyx/Recipes/Lathes/", "/Prototypes/_Onyx/Research/", "/Prototypes/_Onyx/Shaders/",
    ];

    private static readonly string[] LocaleFiles =
    [
        "prototypes/catalog/bounties/xenobiology.ftl", "prototypes/entities/mobs/npcs/slimes.ftl",
        "prototypes/entities/objects/specific/xenobiology/extracts.ftl", "prototypes/entities/objects/tiles/xenobiology.ftl",
        "reagents/xenobiology.ftl", "prototypes/entities/objects/devices/slime-scanner.ftl",
        "prototypes/actions/slime-actions.ftl", "stacks/xenobiology-tiles.ftl",
        "prototypes/entities/clothing/back/xenovac.ftl", "ui/xenobiology-bounty-console.ftl",
        "guidebook/xenobiology.ftl", "prototypes/status-effects/immunities/fire-weather.ftl",
        "prototypes/status-effects/stealth/forced-stealth.ftl", "prototypes/status-effects/npc/faction-override.ftl",
        "prototypes/status-effects/tile-movement.ftl", "time-stop/chronofield.ftl",
        "prototypes/entities/structures/machines/slime-grinder.ftl",
        "prototypes/entities/objects/devices/circuitboards/xenobiology.ftl",
        "prototypes/entities/clothing/belt/xenobiology.ftl", "prototypes/catalog/fills/lockers/xenobiology.ftl",
        "prototypes/entities/structures/machines/computers/xenobiology-bounty.ftl",
        "prototypes/catalog/fills/crates/xenobiology.ftl", "prototypes/catalog/uplink/xenobiology.ftl",
        "prototypes/entities/objects/misc/slime-cubes.ftl", "research/technologies.ftl",
    ];

    [Test]
    [RunOnSide(Side.Client)]
    public void AssetsLocaleGuideAndHelpLinksAreComplete()
    {
        var resources = Pair.Client.ResolveDependency<IResourceManager>();

        foreach (var path in PrototypeDirectories
                     .SelectMany(directory => resources.ContentFindFiles(new ResPath(directory)))
                     .Where(path => path.Extension == "yml"))
        {
            using var reader = resources.ContentFileReadText(path);
            var yaml = reader.ReadToEnd();
            foreach (Match match in RsiPathRegex.Matches(yaml))
                AssertResource(resources, new ResPath($"/Textures/{match.Groups["path"].Value}/meta.json"), path.ToString());
            foreach (Match match in PngPathRegex.Matches(yaml))
                AssertResource(resources, new ResPath($"/Textures/{match.Groups["path"].Value}"), path.ToString());
            foreach (Match match in AudioPathRegex.Matches(yaml))
                AssertResource(resources, new ResPath(match.Groups["path"].Value), path.ToString());
            foreach (Match match in ShaderPathRegex.Matches(yaml))
                AssertResource(resources, new ResPath(match.Groups["path"].Value), path.ToString());
        }

        foreach (var (rsi, requiredStates) in RequiredRsiStates)
        {
            using var reader = resources.ContentFileReadText(new ResPath($"{rsi}/meta.json"));
            using var json = JsonDocument.Parse(reader.ReadToEnd());
            var states = json.RootElement.GetProperty("states").EnumerateArray()
                .Select(state => state.GetProperty("name").GetString())
                .ToHashSet();
            Assert.That(states, Is.SupersetOf(requiredStates), rsi);
        }

        AssertResource(resources, new ResPath("/Audio/_Onyx/Xenobiology/zapbang.ogg"), "orange plasma reaction");
        AssertResource(resources, new ResPath("/ServerInfo/_Onyx/Guidebook/Science/Xenobiology.xml"), "guidebook");
        var localeKeys = new Dictionary<string, HashSet<string>>();
        foreach (var locale in new[] { "en-US", "ru-RU" })
        {
            localeKeys[locale] = [];
            foreach (var file in LocaleFiles)
            {
                var path = new ResPath($"/Locale/{locale}/_Onyx/{file}");
                AssertResource(resources, path, locale);
                using var reader = resources.ContentFileReadText(path);
                foreach (Match match in LocaleKeyRegex.Matches(reader.ReadToEnd()))
                    localeKeys[locale].Add(match.Groups["key"].Value);
            }
        }
        Assert.That(localeKeys["ru-RU"], Is.EquivalentTo(localeKeys["en-US"]), "RU xenobiology keys must not fall back to EN");

        using (var reader = resources.ContentFileReadText(new ResPath("/Locale/ru-RU/_Onyx/prototypes/entities/mobs/npcs/slimes.ftl")))
        {
            var breeds = reader.ReadToEnd();
            foreach (var breed in new[] { "grey", "orange", "purple", "blue", "metal", "yellow", "dark-purple", "dark-blue", "silver", "cerulean", "bluespace", "sepia", "pyrite", "red", "green", "pink", "gold", "oil", "light-pink", "black", "adamantine" })
                Assert.That(breeds, Does.Contain($"xenobio-breed-{breed} ="), breed);
        }

        var guide = CProtoMan.Index(XenobiologyGuide);
        Assert.That(guide.Text.ToString(), Is.EqualTo("/ServerInfo/_Onyx/Guidebook/Science/Xenobiology.xml"));
        Assert.That(CProtoMan.Index(ScienceGuide).Children.Select(child => child.Id), Does.Contain("XenobiologyGuide"));

        foreach (var id in new[] { "BaseMobSlimeXenobio", "BaseSlimeExtract", "SlimeGrinder", "ComputerScienceXenobiologyBounty", "ClothingBackpackXenoBioTank" })
        {
            var prototype = CProtoMan.Index<EntityPrototype>(id);
            Assert.That(prototype.TryComp<GuideHelpComponent>(out var help, CEntMan.ComponentFactory), Is.True, id);
            Assert.That(help!.Guides.Select(entry => entry.Id), Does.Contain("XenobiologyGuide"), id);
        }
    }

    private static void AssertResource(IResourceManager resources, ResPath path, string source)
    {
        Assert.That(resources.TryContentFileRead(path, out var stream), Is.True, $"{source}: {path}");
        stream?.Dispose();
    }
}
