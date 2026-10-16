using System.Collections.Generic;
using UnityEngine;

namespace Jumpline.Level.Chunks
{
    /// <summary>
    /// Prefab başına yığınlı basit havuz: Instantiate/Destroy yerine aç/kapat (mobilde çöp toplayıcı takılması olmaz).
    /// Kopyalar havuz nesnesinin altında durur.
    /// </summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly Transform parent;
        private readonly Dictionary<T, Stack<T>> free = new Dictionary<T, Stack<T>>();
        private readonly Dictionary<T, T> sourceOf = new Dictionary<T, T>();

        public ObjectPool(Transform parent) { this.parent = parent; }

        public T Take(T prefab)
        {
            if (!free.TryGetValue(prefab, out var stack)) free[prefab] = stack = new Stack<T>();
            T item = null;
            while (stack.Count > 0 && item == null) item = stack.Pop();   // yok edilmiş kopyayı atla
            if (item == null) item = Object.Instantiate(prefab, parent);
            sourceOf[item] = prefab;
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;
            item.gameObject.SetActive(false);
            if (sourceOf.TryGetValue(item, out var prefab)) free[prefab].Push(item);
        }

        /// <summary>Isınma: ilk kullanımda takılma olmasın diye önceden kopya üretir.</summary>
        public void Prewarm(T prefab, int count)
        {
            var tmp = new List<T>();
            for (int i = 0; i < count; i++) tmp.Add(Take(prefab));
            foreach (var t in tmp) Release(t);
        }
    }
}
