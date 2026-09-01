namespace Internal {
    // Общая часть описаний групп: имя группы и адрес её ассета в Addressables.
    internal interface ICatalogGroupDefinition {
        string Name { get; }
        string Address { get; set; }
    }
}
