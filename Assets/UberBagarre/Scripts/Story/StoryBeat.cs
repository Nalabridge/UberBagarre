using System;
using System.Collections.Generic;

namespace UberBagarre.Story
{
    /// <summary>
    /// Une réplique : qui parle, ce qu'il dit, et combien de temps ça reste à l'écran.
    ///
    /// La durée est calculée par défaut depuis la longueur du texte plutôt que fixée à la main.
    /// Une durée fixe est toujours fausse pour quelqu'un : trop courte pour qui lit lentement,
    /// interminable pour qui lit vite. Une durée proportionnelle se trompe au moins dans le bon
    /// sens, et le joueur peut de toute façon passer avec la touche d'interaction.
    /// </summary>
    public struct DialogueLine
    {
        public string Speaker;
        public string Text;
        public float Duration;

        /// <summary>Environ 14 caractères par seconde, plancher à 1,6 s.</summary>
        public static DialogueLine Say(string speaker, string text)
        {
            DialogueLine line = new DialogueLine();
            line.Speaker = speaker;
            line.Text = text;
            line.Duration = Math.Max(1.6f, text == null ? 0f : text.Length / 14f);
            return line;
        }

        public static DialogueLine Say(string speaker, string text, float duration)
        {
            DialogueLine line = new DialogueLine();
            line.Speaker = speaker;
            line.Text = text;
            line.Duration = duration;
            return line;
        }
    }

    /// <summary>
    /// Une étape de l'histoire.
    ///
    /// Pourquoi une liste d'étapes écrites en C# et pas des assets à remplir dans l'Inspector :
    /// une étape n'est pas seulement du texte, c'est une CONDITION DE SORTIE — « quand le joueur
    /// a répondu au téléphone », « quand la cible est au sol », « quand trois coups ont porté ».
    /// Ces conditions sont du code. Les sérialiser demanderait un langage de script maison, à
    /// écrire, à déboguer et à documenter, pour un prologue qui tient en vingt étapes. Le coût
    /// serait payé tout de suite et le bénéfice jamais.
    ///
    /// Ce qui compte, en revanche, c'est que la séquence soit LISIBLE d'un seul tenant : la
    /// liste des étapes du prologue se lit comme un synopsis, et c'est elle le document.
    /// </summary>
    public class StoryBeat
    {
        /// <summary>Identifiant lisible, affiché par le diagnostic.</summary>
        public string Id;

        /// <summary>Objectif affiché en haut à droite. Vide = aucun objectif visible.</summary>
        public string Objective;

        /// <summary>Répliques jouées en entrant dans l'étape.</summary>
        public readonly List<DialogueLine> Lines = new List<DialogueLine>();

        /// <summary>Appelé une fois en entrant.</summary>
        public Action OnEnter;

        /// <summary>Appelé une fois en sortant.</summary>
        public Action OnExit;

        /// <summary>
        /// Condition de passage à l'étape suivante. Null = l'étape se termine dès que les
        /// répliques sont finies et que la durée minimale est écoulée.
        /// </summary>
        public Func<bool> IsComplete;

        /// <summary>Durée plancher, même si la condition est déjà remplie.</summary>
        public float MinDuration;

        /// <summary>
        /// Le joueur peut-il bouger et se battre pendant cette étape ?
        /// Une étape de dialogue pur le fige ; une étape d'objectif le laisse libre.
        /// </summary>
        public bool GameplayEnabled = true;

        public StoryBeat(string id)
        {
            Id = id;
        }

        public StoryBeat Say(string speaker, string text)
        {
            Lines.Add(DialogueLine.Say(speaker, text));
            return this;
        }

        public StoryBeat Say(string speaker, string text, float duration)
        {
            Lines.Add(DialogueLine.Say(speaker, text, duration));
            return this;
        }

        public StoryBeat Goal(string objective)
        {
            Objective = objective;
            return this;
        }

        public StoryBeat Until(Func<bool> condition)
        {
            IsComplete = condition;
            return this;
        }

        public StoryBeat Wait(float seconds)
        {
            MinDuration = seconds;
            return this;
        }

        public StoryBeat Freeze()
        {
            GameplayEnabled = false;
            return this;
        }

        public StoryBeat Enter(Action action)
        {
            OnEnter = action;
            return this;
        }

        public StoryBeat Exit(Action action)
        {
            OnExit = action;
            return this;
        }
    }
}
