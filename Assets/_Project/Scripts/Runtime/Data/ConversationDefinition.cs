using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kendo.Data
{
    [Serializable]
    public struct DialogueLine
    {
        public Speaker speaker;
        [TextArea(2, 6)] public string text;
        [Tooltip("Optional voice-over (GDD §15: use voice where possible, subtitles always).")]
        public AudioClip voice;
    }

    /// <summary>A short in-world conversation (GDD §13.2, §15, §16).</summary>
    [CreateAssetMenu(menuName = "Kendo/Data/Conversation", fileName = "Conv_")]
    public sealed class ConversationDefinition : ScriptableObject
    {
        [SerializeField] private string conversationId;
        [SerializeField] private ConversationTrigger trigger;
        [Tooltip("Panel header, e.g. \"YOUR JOURNEY BEGINS\" from the UI mock-ups.")]
        [SerializeField] private string title;
        [SerializeField] private List<DialogueLine> lines = new List<DialogueLine>();

        public string ConversationId => conversationId;
        public ConversationTrigger Trigger => trigger;
        public string Title => title;
        public IReadOnlyList<DialogueLine> Lines => lines;
    }
}
