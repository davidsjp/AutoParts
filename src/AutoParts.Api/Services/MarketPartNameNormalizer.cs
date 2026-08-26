using System.Text.RegularExpressions;

namespace AutoParts.Api.Services;

public sealed class MarketPartNameNormalizer
{
    public const string UncategorizedCategory = "Sem categoria (revisar)";
    public const string IgnoredCategory = "Ignorar - item sem catalogo comercial";

    private static readonly (string Source, string Target)[] TextRepairs =
    [
        ("\u00c3\u00a1", "\u00e1"), ("\u00c3\u00a0", "\u00e0"), ("\u00c3\u00a2", "\u00e2"),
        ("\u00c3\u00a3", "\u00e3"), ("\u00c3\u00aa", "\u00ea"), ("\u00c3\u00a9", "\u00e9"),
        ("\u00c3\u00ad", "\u00ed"), ("\u00c3\u00b3", "\u00f3"), ("\u00c3\u00b4", "\u00f4"),
        ("\u00c3\u00b5", "\u00f5"), ("\u00c3\u00ba", "\u00fa"), ("\u00c3\u00a7", "\u00e7"),
        ("bra?o", "bra\u00e7o"), ("cabe?a", "cabe\u00e7a"), ("fun??es", "fun\u00e7\u00f5es"),
        ("M?dulo", "M\u00f3dulo"), ("m?dulo", "m\u00f3dulo"), ("Tamp?o", "Tamp\u00e3o"),
        ("tamp?o", "tamp\u00e3o"), ("El?trica", "El\u00e9trica"), ("el?trica", "el\u00e9trica"),
        ("Ilumina??o", "Ilumina\u00e7\u00e3o"), ("ilumina??o", "ilumina\u00e7\u00e3o"),
        ("Veda??o", "Veda\u00e7\u00e3o"), ("veda??o", "veda\u00e7\u00e3o"),
        ("Suspens?o", "Suspens\u00e3o"), ("suspens?o", "suspens\u00e3o"),
        ("dire??o", "dire\u00e7\u00e3o"), ("\u00ef\u00bf\u00bd", "\u00e3"), ("\u00c2", "")
    ];

    private static readonly (string Source, string Target)[] Phrases =
    [
        ("Adhesive label", "Etiqueta adesiva"),
        ("Stick-on sign", "Etiqueta adesiva"),
        ("warning sign", "Etiqueta de advert\u00eancia"),
        ("information sign", "Etiqueta informativa"),
        ("read comments and instruct, carefully!", "Consulte as observa\u00e7\u00f5es e instru\u00e7\u00f5es com aten\u00e7\u00e3o"),
        ("operating instructions", "Manual de instru\u00e7\u00f5es"),
        ("installation instructions", "Manual de instala\u00e7\u00e3o"),
        ("Manufacturer certification", "Certifica\u00e7\u00e3o do fabricante"),
        ("Vehicle document portfolio", "Pasta porta-documentos do ve\u00edculo"),
        ("document portfolio", "Pasta porta-documentos"),
        ("repair kit", "Kit de reparo"),
        ("retrofit kit", "Kit de instala\u00e7\u00e3o"),
        ("mounting parts", "Kit de fixa\u00e7\u00e3o"),
        ("mounting material", "Kit de fixa\u00e7\u00e3o"),
        ("set of", "Jogo de"),
        ("light alloy rim", "Roda de liga leve"),
        ("floor mats velours", "Tapetes de veludo"),
        ("floor mat velours", "Tapete de veludo"),
        ("floor carpet", "Carpete do assoalho"),
        ("luggage compartment", "porta-malas"),
        ("main wiring harness", "Chicote el\u00e9trico principal"),
        ("wiring harness", "Chicote el\u00e9trico"),
        ("socket housing", "Carca\u00e7a do conector"),
        ("roof function centre", "M\u00f3dulo de fun\u00e7\u00f5es do teto"),
        ("roof function center", "M\u00f3dulo de fun\u00e7\u00f5es do teto"),
        ("centre console control panel", "Painel de comando do console central"),
        ("center console control panel", "Painel de comando do console central"),
        ("instrument cluster", "Painel de instrumentos"),
        ("instrument panel", "Painel de instrumentos"),
        ("Painel do painel", "Revestimento do painel"),
        ("trim panel dashboard", "Acabamento do painel"),
        ("window regulator", "M\u00e1quina de vidro"),
        ("window lifter", "M\u00e1quina de vidro"),
        ("door lock actuator", "Atuador da fechadura da porta"),
        ("door locking mechanism", "Fechadura da porta"),
        ("door lining", "Forro da porta"),
        ("door trim panel", "Forro da porta"),
        ("outside rear view mirror", "Retrovisor externo"),
        ("outside mirror", "Retrovisor externo"),
        ("rear view mirror", "Retrovisor"),
        ("side view mirror", "Retrovisor externo"),
        ("mirror glass", "Lente do retrovisor"),
        ("engine mounting", "Coxim do motor"),
        ("engine mount", "Coxim do motor"),
        ("transmission mounting", "Coxim do c\u00e2mbio"),
        ("control unit", "M\u00f3dulo eletr\u00f4nico"),
        ("controller", "Controlador"),
        ("audio module", "M\u00f3dulo de \u00e1udio"),
        ("antenna amplifier", "Amplificador de antena"),
        ("head unit", "Unidade multim\u00eddia"),
        ("fuse holder", "Porta-fus\u00edvel"),
        ("fuse box", "Caixa de fus\u00edveis"),
        ("fuel tank", "Tanque de combust\u00edvel"),
        ("fuel pump", "Bomba de combust\u00edvel"),
        ("water pump", "Bomba d'\u00e1gua"),
        ("fan housing with fan", "Defletor do radiador com ventilador"),
        ("piston rings", "An\u00e9is de pist\u00e3o"),
        ("brake disc", "Disco de freio"),
        ("brake pad", "Pastilha de freio"),
        ("brake caliper", "Pin\u00e7a de freio"),
        ("caliper housing", "Carca\u00e7a da pin\u00e7a de freio"),
        ("wheel bearing", "Rolamento de roda"),
        ("wheel hub", "Cubo de roda"),
        ("shock absorber", "Amortecedor"),
        ("coil spring", "Mola helicoidal"),
        ("drive shaft", "Semi-eixo"),
        ("propeller shaft", "Card\u00e3"),
        ("air filter", "Filtro de ar"),
        ("oil filter", "Filtro de \u00f3leo"),
        ("cabin filter", "Filtro de cabine"),
        ("radiator", "Radiador"),
        ("automatic transmission", "C\u00e2mbio autom\u00e1tico"),
        ("traseiro-axle-drive", "Diferencial traseiro"),
        ("rear-axle-drive", "Diferencial traseiro"),
        ("headlight", "Farol"),
        ("tail light", "Lanterna traseira"),
        ("fog light", "Farol de milha"),
        ("bumper cover", "Capa do para-choque"),
        ("bumper panel", "Painel do para-choque"),
        ("front bumper", "Para-choque dianteiro"),
        ("rear bumper", "Para-choque traseiro"),
        ("front door", "Porta dianteira"),
        ("rear door", "Porta traseira"),
        ("door handle", "Ma\u00e7aneta da porta"),
        ("handle", "Ma\u00e7aneta"),
        ("door hinge", "Dobradi\u00e7a da porta"),
        ("door seal", "Borracha de veda\u00e7\u00e3o da porta"),
        ("headrest guide", "Guia do encosto de cabe\u00e7a"),
        ("head rest", "Encosto de cabe\u00e7a"),
        ("head restraint", "Encosto de cabe\u00e7a"),
        ("headrest", "Encosto de cabe\u00e7a"),
        ("seat cover", "Capa do banco"),
        ("backrest cover", "Capa do encosto"),
        ("leather cover", "Capa de couro"),
        ("cloth cover", "Capa de tecido"),
        ("side finisher", "Acabamento lateral"),
        ("lateral trim panel", "Acabamento lateral"),
        ("trunk trim panel", "Acabamento do porta-malas"),
        ("trunk trim", "Acabamento do porta-malas"),
        ("floor trim", "Acabamento do assoalho"),
        ("floor covering", "Revestimento do assoalho"),
        ("rear window shelf", "Tamp\u00e3o traseiro"),
        ("centre arm rest", "Apoio de bra\u00e7o central"),
        ("center arm rest", "Apoio de bra\u00e7o central"),
        ("armrest", "Apoio de bra\u00e7o"),
        ("glove box", "Porta-luvas"),
        ("sun visor", "Quebra-sol"),
        ("decorative strip", "Friso decorativo"),
        ("wood panel", "Acabamento de madeira"),
        ("folding box", "Compartimento rebat\u00edvel"),
        ("vibration absorber", "Absorvedor de vibra\u00e7\u00e3o"),
        ("trunk flap", "Tampa do porta-malas"),
        ("covering cap", "Tampa de acabamento"),
        ("covering", "Revestimento"),
        ("number plate", "Placa de identifica\u00e7\u00e3o"),
        ("emblem", "Emblema"),
        ("badge", "Emblema"),
        ("label", "Etiqueta"),
        ("sign", "Etiqueta"),
        ("cover", "Tampa"),
        ("cap", "Tampa"),
        ("lettering", "Letreiro"),
        ("body carcass with vehicle ID number", "Carroceria com n\u00famero de identifica\u00e7\u00e3o do ve\u00edculo"),
        ("body carcass without vehicle ID number", "Carroceria sem n\u00famero de identifica\u00e7\u00e3o do ve\u00edculo"),
        ("wiper blades", "Palhetas do limpador"),
        ("green windscreen", "Para-brisa verde"),
        ("windscreen", "Para-brisa"),
        ("radio remote control", "Controle remoto do r\u00e1dio"),
        ("Pirelli Cinturato P7 r-f", "Pneu Pirelli Cinturato P7 run-flat"),
        ("Pirelli P-Zero r-f", "Pneu Pirelli P-Zero run-flat"),
        ("Pirelli P Zero r-f", "Pneu Pirelli P Zero run-flat"),
        ("gasket", "Junta de veda\u00e7\u00e3o"),
        ("o-ring", "Anel de veda\u00e7\u00e3o"),
        ("sealing ring", "Anel de veda\u00e7\u00e3o"),
        ("seal", "Retentor"),
        ("rubber mounting", "Coxim de borracha"),
        ("bracket", "Suporte"),
        ("support", "Suporte"),
        ("holder", "Suporte"),
        ("spacer", "Espa\u00e7ador"),
        ("guide", "Guia"),
        ("clip", "Presilha"),
        ("expanding rivet", "Rebite expans\u00edvel"),
        ("clamp", "Abra\u00e7adeira"),
        ("screw", "Parafuso"),
        ("bolt", "Parafuso"),
        ("nut", "Porca"),
        ("washer", "Arruela"),
        ("switch", "Interruptor"),
        ("sensor", "Sensor"),
        ("cable", "Cabo"),
        ("hose", "Mangueira"),
        ("pipe", "Tubo")
    ];

    public string Normalize(string description)
    {
        var value = Repair(description.Trim());
        value = Regex.Replace(value, "[\\\"]", string.Empty);
        value = Regex.Replace(value, "\\s+", " ");

        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var (source, target) in Phrases)
            {
                value = Regex.Replace(value, $"(?<![A-Za-z]){Regex.Escape(source)}(?![A-Za-z])", target,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
        }

        foreach (var (source, target) in new[]
        {
            ("front", "dianteiro"), ("rear", "traseiro"), ("left", "esquerdo"), ("right", "direito"),
            ("outer", "externo"), ("inner", "interno"), ("leather", "couro"), ("cloth", "tecido"),
            ("heated", "aquecido"), ("primed", "com primer"), ("upper", "superior"), ("lower", "inferior"),
            ("middle", "central"), ("centre", "central"), ("center", "central")
        })
        {
            value = Regex.Replace(value, $@"\b{source}\b", target, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return Repair(Regex.Replace(value, "\\s+", " ")).Trim(' ', ',', ';', '-');
    }

    public string NormalizeCategory(string? sourceCategory, string description)
    {
        var normalizedDescription = Normalize(description);
        if (ShouldIgnoreForCommercialCatalog(normalizedDescription)) return IgnoredCategory;

        if (!string.IsNullOrWhiteSpace(sourceCategory)
            && !sourceCategory.Equals("BMV.parts", StringComparison.OrdinalIgnoreCase)
            && !sourceCategory.Equals(UncategorizedCategory, StringComparison.OrdinalIgnoreCase))
        {
            return Normalize(sourceCategory);
        }

        return InferCategory(normalizedDescription) ?? UncategorizedCategory;
    }

    public string? InferCategory(string description)
    {
        var value = Normalize(description).ToLowerInvariant();
        if (ContainsAny(value, "manual", "documento", "certifica\u00e7\u00e3o", "certificate", "portfolio"))
            return "Manuais e documentos";
        if (ContainsAny(value, "retrovisor", "mirror"))
            return "Retrovisores";
        if (ContainsAny(value, "porta", "fechadura", "ma\u00e7aneta", "dobradi\u00e7a", "m\u00e1quina de vidro", "tampa do porta-malas", "door"))
            return "Portas e fechaduras";
        if (ContainsAny(value, "banco", "encosto", "headrest", "seat", "backrest", "capa de couro", "capa do banco"))
            return "Bancos e revestimentos";
        if (ContainsAny(value, "forro", "revestimento", "acabamento", "console", "apoio de bra\u00e7o", "tamp\u00e3o", "porta-luvas", "quebra-sol", "carpete", "friso", "grade", "alto-falante", "tampa"))
            return "Acabamento interno";
        if (ContainsAny(value, "roda", "pneu", "rim", "wheel"))
            return "Rodas";
        if (ContainsAny(value, "para-choque", "bumper"))
            return "Para-choques";
        if (ContainsAny(value, "painel de instrumentos", "revestimento do painel", "dashboard", "head-up display"))
            return "Painel e instrumentos";
        if (ContainsAny(value, "chicote", "conector", "m\u00f3dulo", "controlador", "sensor", "interruptor", "amplificador", "unidade multim\u00eddia", "head unit", "hsd line", "air conditioning control", "fuse"))
            return "El\u00e9trica e m\u00f3dulos";
        if (ContainsAny(value, "motor", "bomba d'\u00e1gua", "filtro de \u00f3leo", "bomba de combust\u00edvel", "tanque de combust\u00edvel", "radiador", "ventilador", "an\u00e9is de pist\u00e3o", "c\u00e2mbio autom\u00e1tico", "engine"))
            return "Motor";
        if (ContainsAny(value, "mangueira", "tubo", "arrefecimento", "coolant", "hose", "pipe"))
            return "Arrefecimento e mangueiras";
        if (ContainsAny(value, "freio", "brake"))
            return "Freios";
        if (ContainsAny(value, "suspens\u00e3o", "amortecedor", "absorvedor de vibra\u00e7\u00e3o", "mola", "dire\u00e7\u00e3o", "semi-eixo", "card\u00e3", "diferencial traseiro", "shock", "spring", "steering"))
            return "Suspens\u00e3o e dire\u00e7\u00e3o";
        if (ContainsAny(value, "veda\u00e7\u00e3o", "junta", "retentor", "anel de veda\u00e7\u00e3o", "coxim de borracha", "grommet", "seal", "gasket"))
            return "Veda\u00e7\u00e3o e juntas";
        if (ContainsAny(value, "parafuso", "porca", "arruela", "presilha", "abra\u00e7adeira", "suporte", "espa\u00e7ador", "rebite", "clip", "bracket", "holder"))
            return "Fixadores e suportes";
        if (ContainsAny(value, "emblema", "placa de identifica\u00e7\u00e3o", "letreiro", "carroceria com n\u00famero", "carroceria sem n\u00famero", "badge"))
            return "Emblemas e identifica\u00e7\u00e3o";
        if (ContainsAny(value, "palhetas do limpador", "para-brisa"))
            return "Vidros e limpadores";
        if (ContainsAny(value, "farol", "lanterna", "light", "lamp", "xenon"))
            return "Ilumina\u00e7\u00e3o";
        if (ContainsAny(value, "tapete", "assoalho", "floor mat"))
            return "Tapetes e assoalho";

        return null;
    }

    public bool ShouldIgnoreForCommercialCatalog(string description)
    {
        var value = Normalize(description).ToLowerInvariant();
        return Regex.IsMatch(value,
            @"\b(template|manual|owner|owners|instruction|instructions|handbook|hdbk|booklet|literature|documentation|certificate|certification|certifica\u00e7\u00e3o|porta-documentos|documento|quick\s*ref|quick\s*reference|sheet\s*insert|license\s*texts|shop\s+at\s+ecs|item\s+number|schablone|form)\b",
            RegexOptions.CultureInvariant);
    }

    private static string Repair(string value)
    {
        foreach (var (source, target) in TextRepairs)
            value = value.Replace(source, target, StringComparison.Ordinal);

        return value;
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
}
