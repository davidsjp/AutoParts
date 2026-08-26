namespace AutoParts.Api.Services;

/// <summary>
/// Deterministic fallback for marketplace descriptions. It does not call AI and
/// deliberately reminds the seller to confirm compatibility by VIN/chassis.
/// </summary>
public sealed class TemplateListingDescriptionService : IListingDescriptionService
{
    public ListingDescriptionResponse Generate(ListingDescriptionInput input)
    {
        var text = $"""
            {input.Title}

            Peça para reposição automotiva. Confira atentamente as fotos, o código OEM e os pontos de fixação antes da compra.

            Código OEM: {input.OemPartNumber}
            Categoria: {input.Category}
            Conteúdo da embalagem: 1 unidade — não é conjunto completo, salvo indicação expressa no anúncio.

            APLICAÇÕES INFORMADAS
            {input.Applications}

            REFERÊNCIAS DE BUSCA
            {input.KeywordGroup}

            IMPORTANTE
            A compatibilidade deve ser confirmada pelo VIN/chassi em catálogo técnico. Compare código OEM, lado, cor, conectores, acabamento e fixações com a peça do seu veículo. Em caso de dúvida, envie os últimos 7 dígitos do chassi antes de finalizar a compra.

            Garantia do vendedor: 30 dias.
            """;
        var verification = input.Source.Equals("RealOEM.com", StringComparison.OrdinalIgnoreCase)
            ? "Descrição montada com dados cadastrados. Confirme o VIN antes de publicar."
            : "Descrição provisória: confirme aplicação e código OEM no RealOEM por VIN antes de publicar.";
        return new ListingDescriptionResponse(text.Trim(), "modelo_local", verification);
    }
}
