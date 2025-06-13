using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System;
using System.Globalization;
using NfseGerenciador.Models;

namespace NfseGerenciador.Services
{
    public class NfseXmlParser
    {
        private readonly LogService _logService;
        public NfseXmlParser(LogService logService)
        {
            _logService = logService;
        }
        public List<Nfse> Parse(string xmlFilePath)
        {
            List<Nfse> nfses = new List<Nfse>();
            try
            {
                XDocument doc = XDocument.Load(xmlFilePath);

                XNamespace ns = "http://www.abrasf.org.br/nfse.xsd";

                foreach (XElement compNfseElement in doc.Descendants(ns + "CompNfse"))
                {
                    XElement nfseElement = compNfseElement.Element(ns + "Nfse");

                    // Verifica se a NF-e foi cancelada antes de tentar parsear
                    XElement nfseCancelamentoElement = compNfseElement.Element(ns + "NfseCancelamento");
                    if (nfseCancelamentoElement != null)
                    {
                        continue;
                    }

                    if (nfseElement != null)
                    {
                        Nfse nfse = new Nfse();

                        XElement infNfseElement = nfseElement.Element(ns + "InfNfse");
                        if (infNfseElement != null)
                        {
                            nfse.Numero = infNfseElement.Element(ns + "Numero")?.Value ?? string.Empty;
                            nfse.CodigoVerificacao = infNfseElement.Element(ns + "CodigoVerificacao")?.Value ?? string.Empty;

                            string dataEmissaoStr = infNfseElement.Element(ns + "DataEmissao")?.Value;
                            if (DateTime.TryParse(dataEmissaoStr, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime dataEmissao))
                            {
                                nfse.DataEmissao = dataEmissao;
                            }
                            else
                            {
                                nfse.DataEmissao = DateTime.MinValue;
                            }

                            string valorLiquidoStr = infNfseElement.Element(ns + "ValoresNfse")?.Element(ns + "ValorLiquidoNfse")?.Value;
                            if (decimal.TryParse(valorLiquidoStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal valorLiquido))
                            {
                                nfse.ValorLiquidoNfse = valorLiquido;
                            }
                            else
                            {
                                nfse.ValorLiquidoNfse = 0m;
                            }
                        }

                        // Dados do Prestador
                        XElement prestadorServicoElement = infNfseElement.Element(ns + "PrestadorServico");
                        if (prestadorServicoElement != null)
                        {
                            nfse.PrestadorCnpj = prestadorServicoElement.Element(ns + "IdentificacaoPrestador")?.Element(ns + "CpfCnpj")?.Element(ns + "Cnpj")?.Value ?? string.Empty;
                            nfse.PrestadorRazaoSocial = prestadorServicoElement.Element(ns + "RazaoSocial")?.Value ?? string.Empty;
                        }
                        else
                        {
                            nfse.PrestadorCnpj = string.Empty;
                            nfse.PrestadorRazaoSocial = string.Empty;
                        }

                        // Dados do Tomador - CORRIGIDO para buscar de infNfseElement
                        XElement declaracaoPrestacaoServicoElement = infNfseElement.Element(ns + "DeclaracaoPrestacaoServico");
                        if (declaracaoPrestacaoServicoElement != null)
                        {
                            XElement infDeclaracaoPrestacaoServicoElement = declaracaoPrestacaoServicoElement.Element(ns + "InfDeclaracaoPrestacaoServico");
                            if (infDeclaracaoPrestacaoServicoElement != null)
                            {
                                XElement tomadorElement = infDeclaracaoPrestacaoServicoElement.Element(ns + "Tomador");
                                if (tomadorElement != null)
                                {
                                    nfse.TomadorRazaoSocial = tomadorElement.Element(ns + "RazaoSocial")?.Value ?? string.Empty;
                                    XElement identificacaoTomadorElement = tomadorElement.Element(ns + "IdentificacaoTomador");
                                    if (identificacaoTomadorElement != null)
                                    {
                                        XElement tomadorCpfCnpjElement = identificacaoTomadorElement.Element(ns + "CpfCnpj");
                                        if (tomadorCpfCnpjElement != null)
                                        {
                                            nfse.TomadorCpfCnpj = tomadorCpfCnpjElement.Element(ns + "Cnpj")?.Value ?? tomadorCpfCnpjElement.Element(ns + "Cpf")?.Value ?? string.Empty;
                                        }
                                        else
                                        {
                                            nfse.TomadorCpfCnpj = string.Empty;
                                        }
                                    }
                                    else
                                    {
                                        nfse.TomadorCpfCnpj = string.Empty;
                                    }
                                }
                                else
                                {
                                    nfse.TomadorCpfCnpj = string.Empty;
                                    nfse.TomadorRazaoSocial = string.Empty;
                                }
                            }
                            else
                            {
                                nfse.TomadorCpfCnpj = string.Empty;
                                nfse.TomadorRazaoSocial = string.Empty;
                            }
                        }
                        else
                        {
                            nfse.TomadorCpfCnpj = string.Empty;
                            nfse.TomadorRazaoSocial = string.Empty;
                        }
                        nfses.Add(nfse);
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"Erro ao fazer parse do arquivo XML: {xmlFilePath}");
            }
            return nfses;
        }
    }
}