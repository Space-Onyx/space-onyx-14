// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems
{
    public sealed partial class ClownAccentSystem : EntitySystem
    {
        [Dependency] private IRobustRandom _random = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<ClownAccentComponent, AccentGetEvent>(OnAccent);
        }

        private static readonly IReadOnlyList<string> LaughsAndExclamations = new List<string>
        {
            " Ой-йо!", " Ха-ха!", " Хи-хи!", " Бугага!", " Хо-хо!", " Вау!", " Бам!", " Бум!"
        }.AsReadOnly();

        private static readonly IReadOnlyDictionary<string, string> SpecialWords = new Dictionary<string, string>()
        {
            { "привет", "хонк" },
            { "пока", "хонк-хонк" },
            { "да", "ага-ага" },
            { "нет", "не-а" },
            { "очень", "супер" },
            { "большой", "огромный" },
            { "маленький", "крошечный" },
            { "смешно", "уморительно" },
        };

        public string Accentuate(string message)
        {
            foreach (var (word, repl) in SpecialWords)
            {
                message = message.Replace(word, repl, StringComparison.OrdinalIgnoreCase);
            }

            if (_random.Prob(0.5f))
            {
                message += _random.Pick(LaughsAndExclamations);
            }

            return message;
        }

        private void OnAccent(EntityUid uid, ClownAccentComponent component, AccentGetEvent args)
        {
            args.Message = Accentuate(args.Message);
        }
    }
}
