using Pb.Builder.Domain.Documents;

namespace Pb.Builder.Application.UseCases;

public sealed record NewDocumentRequest(
    DocumentType DocumentType,
    string DirectoryPath,
    string FileName,
    string BrowserTitle);
