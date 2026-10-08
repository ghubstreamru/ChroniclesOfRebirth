using UnityEngine;

namespace ChroniclesOfRebirth.Gameplay.World
{
    public enum WorldMarkerKind
    {
        /// <summary>Именованная зона (поляна, берег): позже — плашка с названием при входе.</summary>
        Area,
        /// <summary>Ориентир, видимый издалека.</summary>
        Landmark,
        /// <summary>Место под лут.</summary>
        Loot,
        /// <summary>Место под активность (рыбалка, сбор, осмотр).</summary>
        Activity,
        /// <summary>Якорь для улики или квеста.</summary>
        Quest,
        /// <summary>Путь, который откроется позже по сюжету.</summary>
        Blocked,
    }

    /// <summary>
    /// Заготовка под будущие системы (лут, активности, квесты, названия зон).
    /// Сейчас только хранит данные и рисует гизмо в редакторе.
    /// </summary>
    public sealed class WorldMarker : MonoBehaviour
    {
        [SerializeField] WorldMarkerKind kind;
        [SerializeField] string id;
        [Tooltip("Ключ названия в таблице локализации Locations (для зон и ориентиров).")]
        [SerializeField] string nameKey;
        [SerializeField] float radius = 1f;
        [SerializeField, TextArea] string note;

        public WorldMarkerKind Kind => kind;
        public string Id => id;
        public string NameKey => nameKey;
        public float Radius => radius;

        public void Setup(WorldMarkerKind markerKind, string markerId, float markerRadius, string markerNameKey, string markerNote)
        {
            kind = markerKind;
            id = markerId;
            radius = markerRadius;
            nameKey = markerNameKey;
            note = markerNote;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = kind switch
            {
                WorldMarkerKind.Area => new Color(0.4f, 0.8f, 1f, 0.6f),
                WorldMarkerKind.Landmark => new Color(1f, 0.85f, 0.3f, 0.8f),
                WorldMarkerKind.Loot => new Color(1f, 0.75f, 0.1f, 1f),
                WorldMarkerKind.Activity => new Color(0.4f, 1f, 0.5f, 1f),
                WorldMarkerKind.Quest => new Color(0.9f, 0.4f, 1f, 1f),
                _ => new Color(1f, 0.3f, 0.3f, 1f),
            };
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.DrawSphere(transform.position, 0.2f);
        }
    }
}
