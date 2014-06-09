using System;
using System.Configuration;
using System.Net;

using D2L.Extensibility.AuthSdk;

using RestSharp;

namespace ValenceClientSide
{
    class WhoAmIResponse {
        public string Identifier { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string UniqueName { get; set; }
        public string ProfileIdentifier { get; set; }
    }
        
    class Program
    {
        private static ID2LUserContext  InterceptUserTokens( HostSpec host, ID2LAppContext appContext ) {
            // Start HTTP server and listen for the redirect after a successful auth
            var httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://localhost:31337/result/");
            httpListener.Start();
            
            // This call blocks until we get a response
            var ctx = httpListener.GetContext();

            // The LMS returns the user tokens via query parameters to the value provided originally in x_target
            // TODO: deal with "failed to login" case
            var userContext = appContext.CreateUserContext( ctx.Request.Url, host );

            // Send some JavaScript to close the browser popup
            // This is not 100% effective: for example, Firefox will ignore this.
            const string RESPONSE = "<!doctype html><meta charset=\"utf-8\"><script>window.close();</script><h1>You may now close your window</h1><p>You may or may not see this message, depending on your browser</p>";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes( RESPONSE );
            ctx.Response.ContentType = "text/html";
            ctx.Response.ContentLength64 = buffer.Length;
            ctx.Response.OutputStream.Write( buffer, 0, buffer.Length );
            ctx.Response.OutputStream.Close();
            httpListener.Stop();
 
            return userContext;
        }

        private static void OpenBrowser( Uri url ) {
            System.Diagnostics.Process.Start( url.ToString() );
        }

        private static void DoApiStuff( string host, ID2LUserContext userContext ) {
            const string WHOAMI_ROUTE = "/d2l/api/lp/1.0/users/whoami";

            var client = new RestClient( host );
            var valenceAuthenticator = new D2L.Extensibility.AuthSdk.Restsharp.ValenceAuthenticator( userContext );
            var request = new RestRequest( WHOAMI_ROUTE, Method.GET );
            valenceAuthenticator.Authenticate( client, request );

            var response = client.Execute<WhoAmIResponse>( request );

            Console.WriteLine( "Hello, " + response.Data.FirstName + " " + response.Data.LastName );
        }

        static void Main() {
            // This is the LMS we will interact with
            var host = new HostSpec( "https", "lms.valence.desire2learn.com", 443 );
            
            // The appId/appKey come from our app.config - it is good to seperate access keys from the code that uses them.
            // Ideally you wouldn't have production keys committed to source control.
            string appId = ConfigurationManager.AppSettings["appId"];
            string appKey = ConfigurationManager.AppSettings["appKey"];

            // This is the port we will temporarily host a server on to intercept the user tokens after a successful login
            int port = int.Parse( ConfigurationManager.AppSettings["serverPort"] );

            // Create url for the user to login. If they have already done so they will not actually have to type their password (maybe).
            var appContextFactory = new D2LAppContextFactory();
            var appContext = appContextFactory.Create( appId, appKey );
            var authUrl = appContext.CreateUrlForAuthentication( host, new Uri( "http://localhost:" + port + "/result/" ) );

            OpenBrowser( authUrl );

            // This call will block until we have a result
            // TODO: you'll want better control flow and error handling here
            var userContext = InterceptUserTokens( host, appContext );

            // Now we can call Valence
            DoApiStuff( host.Scheme + "://" + host.Host + ":" + host.Port, userContext );

            // Pause the terminal
            Console.ReadKey();
        }
    }
}
