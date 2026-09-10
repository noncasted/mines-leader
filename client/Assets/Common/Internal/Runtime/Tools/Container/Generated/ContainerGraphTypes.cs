namespace Internal {
    public enum ContainerGraphOrigin {
        ConstructedType = 0,
        PrefabInstance = 1,
        PrefabAsset = 2,
        InstanceHole = 3,
        ParameterHole = 4,
        SwitchFactory = 5,
        Alternative = 6,
        InjectExisting = 7,
        ExternalInstaller = 8,
        SceneServices = 9,
    }

    public readonly struct ContainerGraphSwitchArm {
        public readonly string Discriminant;
        public readonly string ImplementationType;
        public readonly string ParameterExpression;

        public ContainerGraphSwitchArm(string discriminant, string implementationType, string parameterExpression) {
            Discriminant = discriminant ?? "";
            ImplementationType = implementationType ?? "";
            ParameterExpression = parameterExpression ?? "";
        }
    }

    public readonly struct ContainerGraphRegistration {
        public readonly string Kind;
        public readonly string ImplementationType;
        public readonly string[] ServiceTypes;
        public readonly string Lifetime;
        public readonly ContainerGraphOrigin Origin;
        public readonly string Source;
        public readonly string Hole;
        public readonly string File;
        public readonly int Line;
        public readonly ContainerGraphSwitchArm[] Arms;

        public ContainerGraphRegistration(
            string kind,
            string implementationType,
            string[] serviceTypes,
            string lifetime,
            ContainerGraphOrigin origin,
            string source,
            string hole,
            string file,
            int line,
            ContainerGraphSwitchArm[] arms) {
            Kind = kind ?? "";
            ImplementationType = implementationType ?? "";
            ServiceTypes = serviceTypes ?? new string[0];
            Lifetime = lifetime ?? "";
            Origin = origin;
            Source = source ?? "";
            Hole = hole ?? "";
            File = file ?? "";
            Line = line;
            Arms = arms ?? new ContainerGraphSwitchArm[0];
        }
    }

    public readonly struct ContainerGraphMethod {
        public readonly string Id;
        public readonly bool IsRoot;
        public readonly string File;
        public readonly int Line;
        public readonly ContainerGraphRegistration[] Registrations;
        public readonly string[] Calls;

        public ContainerGraphMethod(
            string id,
            bool isRoot,
            string file,
            int line,
            ContainerGraphRegistration[] registrations,
            string[] calls) {
            Id = id ?? "";
            IsRoot = isRoot;
            File = file ?? "";
            Line = line;
            Registrations = registrations ?? new ContainerGraphRegistration[0];
            Calls = calls ?? new string[0];
        }
    }

    public readonly struct ContainerGraphAsset {
        public readonly string AssetPath;
        public readonly string HolderType;
        public readonly string[] ComponentTypes;

        public ContainerGraphAsset(string assetPath, string holderType, string[] componentTypes) {
            AssetPath = assetPath ?? "";
            HolderType = holderType ?? "";
            ComponentTypes = componentTypes ?? new string[0];
        }
    }
}
