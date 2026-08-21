using System.Text.RegularExpressions;

namespace AutoParts.Api.Services;

/// <summary>
/// Conservative technical glossary for BMV catalogue names. It intentionally leaves
/// unknown text untouched: a misleading Portuguese name is worse than a source name
/// that still needs an AI/RealOEM review.
/// </summary>
public sealed class MarketPartNameNormalizer
{
    private static readonly (string Source, string Target)[] Phrases =
    [
        ("Adesivo Etiqueta, Trocador de CD", "Etiqueta adesiva do trocador de CD"),
        ("Adesivo Etiqueta", "Etiqueta adesiva"),
        ("Hex head Parafuso", "Parafuso sextavado"),
        ("Hex Parafuso with Arruela", "Parafuso sextavado com arruela"),
        ("Hex Parafuso", "Parafuso sextavado"),
        ("Hex Porca", "Porca sextavada"),
        ("Parafuso, self tapping", "Parafuso autoatarraxante"),
        ("self tapping", "autoatarraxante"),
        ("with Arruela", "com arruela"),
        ("snap ring", "Anel elástico"),
        ("circlip", "Anel elástico"),
        ("retaining ring", "Anel de retenção"),
        ("coolant hose", "Mangueira do arrefecimento"),
        ("brake hose", "Mangueira de freio"),
        ("fuel hose", "Mangueira de combustível"),
        ("vacuum hose", "Mangueira de vácuo"),
        ("hose clamp", "Abraçadeira de mangueira"),
        ("rubber mount", "Coxim de borracha"),
        ("bush bearing", "Bucha"),
        ("read comments and instruct, carefully!", "Consulte as observações e instruções com atenção"),
        ("operating instructions", "Manual de instruções"),
        ("load-through system locking sign", "Etiqueta do travamento do sistema passa-objetos"),
        ("load through system locking sign", "Etiqueta do travamento do sistema passa-objetos"),
        ("cd changer", "Trocador de CD"),
        ("stick-on", "Adesivo"),
        ("warning sign", "Etiqueta de advertência"),
        ("information sign", "Etiqueta informativa"),
        ("repair kit", "Kit de reparo"),
        ("retrofit kit", "Kit de instalação"),
        ("mounting parts", "Kit de fixação"),
        ("mounting material", "Kit de fixação"),
        ("set of", "Jogo de"),
        ("left hand", "Esquerdo"),
        ("right hand", "Direito"),
        ("front axle", "Eixo dianteiro"),
        ("rear axle", "Eixo traseiro"),
        ("wiring harness", "Chicote elétrico"),
        ("window regulator", "Máquina de vidro"),
        ("window lifter", "Máquina de vidro"),
        ("door lock actuator", "Atuador da fechadura da porta"),
        ("door locking mechanism", "Fechadura da porta"),
        ("door trim panel", "Forro da porta"),
        ("outside rear view mirror", "Retrovisor externo"),
        ("rear view mirror", "Retrovisor"),
        ("side view mirror", "Retrovisor externo"),
        ("engine mounting", "Coxim do motor"),
        ("engine mount", "Coxim do motor"),
        ("transmission mounting", "Coxim do câmbio"),
        ("control unit", "Módulo eletrônico"),
        ("fuse holder", "Porta-fusível"),
        ("fuse box", "Caixa de fusíveis"),
        ("fuel tank", "Tanque de combustível"),
        ("fuel pump", "Bomba de combustível"),
        ("water pump", "Bomba d'água"),
        ("brake disc", "Disco de freio"),
        ("brake pad", "Pastilha de freio"),
        ("brake caliper", "Pinça de freio"),
        ("wheel bearing", "Rolamento de roda"),
        ("wheel hub", "Cubo de roda"),
        ("shock absorber", "Amortecedor"),
        ("coil spring", "Mola helicoidal"),
        ("drive shaft", "Semi-eixo"),
        ("propeller shaft", "Cardã"),
        ("air filter", "Filtro de ar"),
        ("oil filter", "Filtro de óleo"),
        ("cabin filter", "Filtro de cabine"),
        ("headlight", "Farol"),
        ("tail light", "Lanterna traseira"),
        ("fog light", "Farol de milha"),
        ("bumper cover", "Capa do para-choque"),
        ("front bumper", "Para-choque dianteiro"),
        ("rear bumper", "Para-choque traseiro"),
        ("front door", "Porta dianteira"),
        ("rear door", "Porta traseira"),
        ("door handle", "Maçaneta da porta"),
        ("door hinge", "Dobradiça da porta"),
        ("door seal", "Borracha de vedação da porta"),
        ("number plate", "Placa de identificação"),
        ("emblem", "Emblema"),
        ("badge", "Emblema"),
        ("label", "Etiqueta"),
        ("sign", "Etiqueta"),
        ("cover", "Tampa"),
        ("gasket", "Junta de vedação"),
        ("seal", "Retentor"),
        ("bracket", "Suporte"),
        ("holder", "Suporte"),
        ("clip", "Presilha"),
        ("clamp", "Abraçadeira"),
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
        var value = Regex.Replace(description.Trim(), "[\\\"“”]", string.Empty);
        value = Regex.Replace(value, "\\s+", " ");
        // A second pass resolves cases such as "Stick-on sign": the first pass
        // produces "Adesivo Etiqueta" and the second turns it into the market name.
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var (source, target) in Phrases)
            {
                value = Regex.Replace(value, $"(?<![A-Za-z]){Regex.Escape(source)}(?![A-Za-z])", target,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
        }
        return value.Trim();
    }

    public bool ShouldIgnoreForCommercialCatalog(string description)
    {
        var value = description.ToLowerInvariant();
        return Regex.IsMatch(value, @"\b(parafuso|porca|arruela|mangueira|screw|bolt|nut|washer|hose)\b",
            RegexOptions.CultureInvariant);
    }
}
