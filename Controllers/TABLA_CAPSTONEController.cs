using Capstone_Integracion_2.Models;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.IO;

public class TABLA_CAPSTONEController : Controller
{
    private CapstoneDBEntities db = new CapstoneDBEntities();

    // Vista inicial para ingresar la empresa
    public ActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<ActionResult> StartTests(string companyName)
    {
        // Crear filas en la base de datos para la empresa
        for (int tecnica = 1; tecnica <= 3; tecnica++)
        {
            db.TABLA_CAPSTONE.Add(new TABLA_CAPSTONE
            {
                Tecnica = tecnica,
                Estado = "Fallida",
                Empresa = companyName
            });
        }
        db.SaveChanges();

        // Iniciar los builds en TeamCity y obtener los enlaces de descarga
        var artifactLinks = await TriggerBuildsAndGetArtifactLinks();

        // Devolver los enlaces de descarga para mostrarlos en la vista
        ViewBag.ArtifactLinks = artifactLinks;
        return View("ArtifactLinks"); // Vista para mostrar los enlaces de descarga
    }

    private async Task<string[]> TriggerBuildsAndGetArtifactLinks()
    {
        // Cambia estos valores por los de TeamCity

        string[] buildConfigIds = { "Capstone_Build", "Capstone_Build", "Capstone_Build" }; // Cambia los IDs de configuración según TeamCity
        string baseUrl = "http://localhost:8111"; // URL de TeamCity
        string token = "eyJ0eXAiOiAiVENWMiJ9.N2h6VWVmMHN1YUVEbzZ0V2JTa3lWYXRLLUtZ.N2U5N2U4NmUtZTBmYy00ZDE4LWFmNGYtZDg4ZTJkNGJkYTQw"; // Reemplaza con tu token de autenticación

        string[] artifactLinks = new string[3];
        using (var client = new HttpClient())
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

            for (int i = 0; i < buildConfigIds.Length; i++)
            {
                try
                {
                    // Iniciar el build
                    Console.WriteLine($"Iniciando build para configuración: {buildConfigIds[i]}");
                    string buildId = await RunBuild(client, buildConfigIds[i]);

                    // Obtener enlace del artefacto
                    if (!string.IsNullOrEmpty(buildId))
                    {
                        Console.WriteLine($"Build iniciado con éxito. ID de Build: {buildId}");
                        artifactLinks[i] = await GetArtifactDownloadLink(client, buildConfigIds[i], buildId);
                    }
                    else
                    {
                        Console.WriteLine($"Error: No se pudo obtener el ID de build para {buildConfigIds[i]}.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en la configuración de build {buildConfigIds[i]}: {ex.Message}");
                    artifactLinks[i] = null;
                }
            }
        }
        return artifactLinks;
    }

    private async Task<string> RunBuild(HttpClient client, string buildConfigId)
    {
        string xmlPayload = $"<build><buildType id=\"{buildConfigId}\"/></build>";
        var content = new StringContent(xmlPayload, Encoding.UTF8, "application/xml");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync("/app/rest/buildQueue", content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al realizar la solicitud a TeamCity: {ex.Message}");
            return null;
        }

        if (response.IsSuccessStatusCode)
        {
            string responseData = await response.Content.ReadAsStringAsync();
            return ExtractBuildId(responseData);
        }
        else
        {
            Console.WriteLine($"Error al iniciar el build. Código de estado: {response.StatusCode}, Respuesta: {await response.Content.ReadAsStringAsync()}");
            return null;
        }
    }

    private string ExtractBuildId(string responseData)
    {
        const string buildIdTag = "id=\"";
        var idIndex = responseData.IndexOf(buildIdTag);
        if (idIndex != -1)
        {
            idIndex += buildIdTag.Length;
            var endIndex = responseData.IndexOf("\"", idIndex);
            if (endIndex != -1)
            {
                return responseData.Substring(idIndex, endIndex - idIndex);
            }
        }
        return null;
    }

    private async Task<string> GetArtifactDownloadLink(HttpClient client, string buildConfigId, string buildId)
    {
        // http://localhost:8111/repository/download/Capstone_Build/31:id/Ldr.exe
        string artifactUrl = $"/repository/download/{buildConfigId}/{buildId}:id/ldr.exe";
        //string artifactUrl = $"/app/rest/builds/id:{buildId}/artifacts/content/";
        int attempts = 0;
        string finalArtifactLink = null;

        // Intentar obtener el enlace del artefacto con reintentos
        while (attempts < 10)
        {
            Console.WriteLine($"Intentando obtener el enlace del artefacto. Intento {attempts + 1}/10");

            var response = await client.GetAsync(artifactUrl);
            if (response.IsSuccessStatusCode)
            {
                finalArtifactLink = $"{client.BaseAddress}{artifactUrl}";
                Console.WriteLine($"Enlace del artefacto obtenido: {finalArtifactLink}");
                break;
            }
            else
            {
                Console.WriteLine($"El artefacto aún no está disponible. Código de estado: {response.StatusCode}");
                attempts++;
                await Task.Delay(5000); // Esperar 5 segundos antes de intentar nuevamente
            }
        }
        return finalArtifactLink;
    }
}