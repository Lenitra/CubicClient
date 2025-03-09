using UnityEngine;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Cubic.V1.Crypto;
using Cubic.V1.Formatter;
using Cubic.V1.Parser;
using Cubic.V1.Protocol.Auth;
using Cubic.V1.Protocol.World;
using Google.Protobuf;

public class CubicTestMonoBehaviour : MonoBehaviour
{
    private string logFilePath;

    private void Awake()
    {
        // Définition du chemin du fichier log dans le dossier persistant
        logFilePath = Path.Combine(Application.persistentDataPath, "log.txt");
        // Optionnel : on efface le fichier précédent au démarrage
        File.WriteAllText(logFilePath, "Log démarré le " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine);
    }

    private async void Start()
    {
        WriteLog("[Start] Démarrage de la connexion WebSocket.");
        await Run();
    }

    private async Task Run()
    {
        try
        {
            // Génération du wallet
            WriteLog("[Run] Génération du wallet...");
            Ed25519Wallet wallet = Ed25519Wallet.GenerateKeyPairFromPassphrase("hello world", "my password");
            WriteLog("[Run] Wallet généré. Clé publique : " + BitConverter.ToString(wallet.PublicKey));

            // Instanciation du parser et du formatter JSON
            WriteLog("[Run] Création du parser et du formatter JSON.");
            JsonProtocolParser parser = new JsonProtocolParser();
            JsonProtocolFormatter formatter = new JsonProtocolFormatter();

            // Création et connexion du ClientWebSocket
            WriteLog("[Run] Création du ClientWebSocket...");
            ClientWebSocket client = new ClientWebSocket();
            WriteLog("[Run] Connexion à ws://212.227.52.171:3030/ws...");
            await client.ConnectAsync(new Uri("ws://212.227.52.171:3030/ws"), default);
            WriteLog("[Run] Connexion établie.");

            // Envoi de la requête d'authentification initiale
            WriteLog("[Run] Préparation de HelloAuthRequest...");
            string json = formatter.WriteProtocol(new HelloAuthRequest());
            WriteLog("[Run] HelloAuthRequest JSON : " + json);
            ArraySegment<byte> sendBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
            WriteLog("[Run] Envoi de HelloAuthRequest...");
            await client.SendAsync(sendBuffer, WebSocketMessageType.Text, true, default);
            WriteLog("[Run] HelloAuthRequest envoyé.");

            // Boucle de traitement des messages entrants
            while (true)
            {
                WriteLog("[Run] Attente de réception d'un message...");
                ArraySegment<byte> recvBuffer = new ArraySegment<byte>(new byte[2048]);
                WebSocketReceiveResult result = await client.ReceiveAsync(recvBuffer, default);
                WriteLog("[Run] Message reçu. Octets reçus : " + result.Count);

                // Lecture et enregistrement de la réponse
                string response = Encoding.UTF8.GetString(recvBuffer.Array, 0, result.Count);
                WriteLog("[Run] Réponse reçue : " + response);

                // Décodage du message
                IMessage message = parser.ReadProtocol(response);
                WriteLog("[Run] Message décodé, type : " + message.GetType().Name);

                if (message is HelloAuthResponse har)
                {
                    WriteLog("[Run] HelloAuthResponse reçu.");
                    // Signature de la clé et envoi de la demande de connexion
                    WriteLog("[Run] Signature en cours avec la clé reçue...");
                    byte[] signature = wallet.Sign(har.Key.ToByteArray());
                    WriteLog("[Run] Signature générée : " + BitConverter.ToString(signature));

                    ConnectRequest connectRequest = new ConnectRequest()
                    {
                        Signature = ByteString.CopyFrom(signature),
                        PublicKey = ByteString.CopyFrom(wallet.PublicKey)
                    };
                    json = formatter.WriteProtocol(connectRequest);
                    WriteLog("[Run] ConnectRequest JSON : " + json);
                    sendBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
                    WriteLog("[Run] Envoi de ConnectRequest...");
                    await client.SendAsync(sendBuffer, WebSocketMessageType.Text, true, default);
                    WriteLog("[Run] ConnectRequest envoyé.");
                }
                else if (message is ConnectResponse)
                {
                    WriteLog("[Run] ConnectResponse reçu.");
                    // Envoi de la requête "Hello World"
                    WriteLog("[Run] Préparation de HelloWorldRequest...");
                    json = formatter.WriteProtocol(new HelloWorldRequest());
                    WriteLog("[Run] HelloWorldRequest JSON : " + json);
                    sendBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
                    WriteLog("[Run] Envoi de HelloWorldRequest...");
                    await client.SendAsync(sendBuffer, WebSocketMessageType.Text, true, default);
                    WriteLog("[Run] HelloWorldRequest envoyé.");
                }
                else
                {
                    WriteLog("[Run] Message inconnu reçu : " + message.GetType().Name);
                }
            }
        }
        catch (Exception ex)
        {
            WriteLog("[Run] Exception rencontrée : " + ex.Message);
        }
    }

    private void WriteLog(string message)
    {
        try
        {
            string logEntry = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine;
            File.AppendAllText(logFilePath, logEntry);
        }
        catch (Exception e)
        {
            // Si l'écriture dans le fichier échoue, on peut afficher une erreur dans la console Unity en dernier recours.
            Debug.LogError("Erreur lors de l'écriture du log : " + e.Message);
        }
    }
}
