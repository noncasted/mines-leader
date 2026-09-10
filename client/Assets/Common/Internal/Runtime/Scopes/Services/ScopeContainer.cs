using System;

namespace Internal
{
    public static class ScopeContainer
    {
        // Контейнер скоупа — только сгенерированный класс. Нет класса — исключение, фолбэка нет.
        public static IContainer Create(string rootId, ContainerBuilder builder)
        {
            ContainerThread.Assert();

            if (string.IsNullOrEmpty(rootId) == true)
                throw new ArgumentException("Root id is required.", nameof(rootId));

            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            return GeneratedScopes.Create(rootId, builder);
        }

        // Сущность: класс варианта выбирается по конкретному типу вьюхи (locked 14).
        public static IContainer CreateEntity(string rootId, Type viewType, ContainerBuilder builder)
        {
            if (viewType != null)
            {
                var variant = GeneratedScopes.VariantKey(rootId, viewType);

                if (GeneratedScopes.IsRegistered(variant) == true)
                    return Create(variant, builder);
            }

            return Create(rootId, builder);
        }
    }
}
