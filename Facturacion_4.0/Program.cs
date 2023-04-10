using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;
using Newtonsoft.Json;
using Facturacion4_0.Utils;

namespace Facturacion4_0
{
    class Program
    {
        //Rutas
        static private string path = @"C:\Users\GRUPO LAHE\Documents\Proyectos\Facturacion_4.0\";//Ruta Principal de proyecto.
        static string pathXML = path + @"Facturacion_4.0\XmlSinTimbrar.xml";//Ruta donde guardar xml no timbrado.
        static string pathXMLFinal = path + @"Facturacion_4.0\XmlTimbrado.xml";//Ruta donde guardar xml timbrado.

        static void Main(string[] args)
        {
            Comprobante oComprobante = null;//Se inializa el documento(factura)
            if (args != null && args.Count() > 0 && args[0] != null) //Se esperan parametros para la integración con php u otro.
            {
                try
                {
                    //espacios se pierden en los comandos por ello se puso ■ y ahora se obtienen de nuevo los espacios.
                    args[0] = args[0].Replace("■", " ");
                    //Console.WriteLine("c#: " + args[0]);
                    oComprobante = JsonConvert.DeserializeObject<Comprobante>(args[0]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("error a deserializar" + ex.Message);
                    return;
                }

            }

            //Obtener # certificado, esto la fiel del emsior.
            string pathCer = path + @"Facturacion_4.0\CSD01_AAA010101AAA.cer";
            string pathKey = path + @"Facturacion_4.0\CSD01_AAA010101AAA.key";
            string clavePrivada = "12345678a";
            string numeroCertificado, aa, b, c;
            SelloDigital.leerCER(pathCer, out aa, out b, out c, out numeroCertificado);//Por medio de la clase selloDigital, obtenemos el # de certificado del archivo .cer (esta fue creada por alguien externo).

            //Llenado de la clase comprobante (factura).
            if (oComprobante == null)
            {
                oComprobante = new Comprobante();
                oComprobante.NoCertificado = numeroCertificado;
                oComprobante.Version = "4.0";
                oComprobante.Serie = "H";
                oComprobante.Folio = "1";
                oComprobante.Fecha = (DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")).ToString();
                oComprobante.FormaPago = c_FormaPago.Item99;
                oComprobante.SubTotal = 10m;
                oComprobante.Descuento = 1;
                oComprobante.Moneda = c_Moneda.MXN;
                oComprobante.Total = 9;
                oComprobante.TipoDeComprobante = c_TipoDeComprobante.I;
                oComprobante.MetodoPago = c_MetodoPago.PUE;
                oComprobante.LugarExpedicion = "20131";
                //Datos obligatorios para cfdi 4.0, basado en el anexo 20.

                //Datos de empresa.
                ComprobanteEmisor oEmisor = new ComprobanteEmisor();
                oEmisor.Rfc = "LEGH870601DM7";
                oEmisor.Nombre = "Empresa SA de CV";
                oEmisor.RegimenFiscal = c_RegimenFiscal.Item601;

                //Datos del cliente.
                ComprobanteReceptor oReceptor = new ComprobanteReceptor();
                oReceptor.Nombre = "Cliente SA DE CV";
                oReceptor.Rfc = "BIO091204LB1";
                oReceptor.UsoCFDI = c_UsoCFDI.P01;
                oReceptor.RegimenFiscalReceptor = c_RegimenFiscal.Item601;
                oReceptor.DomicilioFiscalReceptor = "54900";

                //Se añade emisor(empresa) y receptor(cliente) al comprobante(factura).
                oComprobante.Emisor = oEmisor;
                oComprobante.Receptor = oReceptor;

                //Datos sobre la compra o interacción cliente-empresa.
                List<ComprobanteConcepto> lstConceptos = new List<ComprobanteConcepto>();
                ComprobanteConcepto oConcepto = new ComprobanteConcepto();
                oConcepto.Importe = 10m;
                oConcepto.ClaveProdServ = "10101506";
                oConcepto.Cantidad = 1;
                oConcepto.ClaveUnidad = "C81";
                oConcepto.Descripcion = "Congreso de prueba";
                oConcepto.ValorUnitario = 10m;
                oConcepto.Descuento = 1;
                lstConceptos.Add(oConcepto);//Se agrega a un arreglo para en caso de ser mas conceptos.

                //Se añade el listado de conceptos al comprobante(factura).
                oComprobante.Conceptos = lstConceptos.ToArray();
            }

            CreateXML(oComprobante);//Creamos el xml

            string cadenaOriginal = "";
            string pathxsl = path + @"Facturacion_4.0\xslt40\cadenaoriginal_4_0.xslt";
            System.Xml.Xsl.XslCompiledTransform transformador = new System.Xml.Xsl.XslCompiledTransform(true);
            transformador.Load(pathxsl);

            using (StringWriter sw = new StringWriter())
            using (XmlWriter xwo = XmlWriter.Create(sw, transformador.OutputSettings))
            {

                transformador.Transform(pathXML, xwo);//Pasará el xml a la cadena original que pide SAT.
                cadenaOriginal = sw.ToString();
            }

            //Proceso para sellar la factura
            SelloDigital oSelloDigital = new SelloDigital();
            oComprobante.Certificado = oSelloDigital.Certificado(pathCer);//Prepara parte del sello con obtener el certificado.
            oComprobante.Sello = oSelloDigital.Sellar(cadenaOriginal, pathKey, clavePrivada);//Se sella con cadena original, llave de fiel y contraseña.

            CreateXML(oComprobante);

            //TIMBRE DEL XML
            /*
            ServiceReference_pruebatimbrado.RespuestaCFDi respuestaCFDI = new ServiceReference_pruebatimbrado.RespuestaCFDi();

            byte[] bXML = System.IO.File.ReadAllBytes(pathXML);

            ServiceReference_pruebatimbrado.TimbradoClie nt oTimbrado =new ServiceReference_pruebatimbrado.TimbradoClient();

            //enviar maila ipintor@bioxor.com para comprar folios y utilizar este PAC
            respuestaCFDI = oTimbrado.TimbrarTest("tuusuariodelPAC", "tucontraseñadelPAC", bXML);

            if (respuestaCFDI.Documento == null) {
                Console.WriteLine(respuestaCFDI.Mensaje);
            }
            else
            {

                System.IO.File.WriteAllBytes(pathXMLFinal, respuestaCFDI.Documento);
            }
            */
            //Timbrado ejemplo
            /*
                bool produccion = false;
                string prod_endpoint = "TimbradoEndpoint_PRODUCCION";
                string test_endpoint = "TimbradoEndpoint_TESTING";
                System.Net.ServicePointManager.Expect100Continue = false;//Si recibe error 417 deberá descomentar la linea a continuación

                ServiceReference1.TimbradoPortTypeClient portClient = null;
                portClient = (produccion)
                    ? new ServiceReference1.TimbradoPortTypeClient(prod_endpoint)
                    : portClient = new ServiceReference1.TimbradoPortTypeClient(test_endpoint);

                try
                {
                    Console.WriteLine("Exito: ----"+ pathXML);
                    //byte[] bytes = System.IO.File.ReadAllBytes(pathXML);
                    byte[] bytes = Encoding.UTF8.GetBytes(System.IO.File.ReadAllText(pathXML));
                    System.Console.WriteLine("Sending request...");
                    System.Console.WriteLine("EndPoint = " + portClient.Endpoint.Address);
                    ServiceReference1.CFDICertificacion response = portClient.timbrar("testing@solucionfactible.com", "timbrado.SF.16672", bytes, false);

                    System.Console.WriteLine("Información de la transacción");
                    System.Console.WriteLine(response.status);
                    System.Console.WriteLine(response.mensaje);
                    System.Console.WriteLine("Resultados recibidos" + response.resultados.Length);
                    ServiceReference1.CFDIResultadoCertificacion[] resultados = response.resultados;
                    Console.ReadLine();
                    //Clases a usar en cancelación:
                    //com.sf.ws.Timbrado.CFDICancelacion
                    //com.sf.ws.Timbrado.CFDIResultadoCancelacion

                }
                catch (Exception ex)
                {
                    System.Console.WriteLine(ex.StackTrace);
                    Console.ReadLine();
                }
            */
            //Fin timbrado ejemplo
        }

        private static void CreateXML(Comprobante oComprobante)
        {
            //Serialización del comprobante(factura) previamente creado por las clases a xml.
            XmlSerializerNamespaces xmlNameSpace = new XmlSerializerNamespaces();
            xmlNameSpace.Add("cfdi", "http://www.sat.gob.mx/cfd/4");//Cambia dependiendo de la versión.
            xmlNameSpace.Add("tfd", "http://www.sat.gob.mx/TimbreFiscalDigital");
            xmlNameSpace.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");
            xmlNameSpace.Add("xs", "http://www.w3.org/2001/XMLSchema");

            XmlSerializer oXmlSerializar = new XmlSerializer(typeof(Comprobante));//lee la clase comprobante(factura) creada.
            string sXml = "";
            using (var sww = new StringWriterWithEncoding(Encoding.UTF8))//Codifica a UTF-8 por medio de la clase heredada encoding
            {
                using (XmlWriter writter = XmlWriter.Create(sww))
                {
                    oXmlSerializar.Serialize(writter, oComprobante, xmlNameSpace);
                    sXml = sww.ToString();
                }
            }
            System.IO.File.WriteAllText(pathXML, sXml);//Se guarda el string en un archivo
        }
    }
}
