﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using PublishSubscribeBroker.Networking;

namespace PublishSubscribeBroker
{
    /// <summary>
    /// Specialized client to act as a subscriber in a publish-subscribe system
    /// </summary>
    class SubscriberClient : Client
    {
        /// <summary>
        /// The readable name of the subscriber client
        /// </summary>
        public string Name { get; set; } = "anonymous";

        /// <summary>
        /// A client-side cached list of topics available on the broker server
        /// </summary>
        public List<NameIdPair> TopicCache { get; private set; }

        /// <summary>
        /// A thread-safe queue of pending requests from the user to be sent sequentially to the server
        /// </summary>
        // Note: Thread-safe to allow a separate thread to accept user input and add requests asynchronously
        private ConcurrentQueue<Request> pendingRequests;

        /// <summary>
        /// Whether the client is currently waiting for a response from the server or not
        /// </summary>
        private bool waitingForResponse = false;

        public SubscriberClient(string ipAddress, int port) : base(ipAddress, port)
        {
            pendingRequests = new ConcurrentQueue<Request>();
        }

        /// <summary>
        /// Override method to handle the publish-subscribe protocol on the subscriber side
        /// </summary>
        /// <param name="stream">The network stream used for communication with the server</param>
        protected override void HandleProtocol(NetworkStream stream)
        {
            // Attempt to receive a message or response from the server
            TryReceive(stream);

            // Attempt to send a request to the server (if one has been initiated)
            TrySend(stream);
        }

        /// <summary>
        /// Attempt to receive data from the server if there is data to be read
        /// </summary>
        /// <param name="stream">The network stream used for communication with the server</param>
        protected void TryReceive(NetworkStream stream)
        {
            if (stream.DataAvailable)
            {
                // Receive and process a response object from the server
                Response response = ReceiveMessage<Response>(stream);
                ProcessResponse(response);
            }
        }

        /// <summary>
        /// Process a response received from the server and take action based on the response type
        /// </summary>
        /// <param name="response">The received response to process</param>
        protected void ProcessResponse(Response response)
        {
            if (response.Type == ResponseType.NEW_MESSAGE)
            {
                // Show a newly published message from the broker
                Message<string> message = (response as NewMessageResponse<string>).Message;

                Console.WriteLine("[New Message from \"{0}\" in topic \"{1}\"]" + Environment.NewLine + "{2}",
                    message.PublisherInfo.Name, message.TopicInfo.Name, message.Content);
            }
            else if (waitingForResponse)
            {
                // Handle responses to previous client requests
                if (response.Type == ResponseType.LIST_TOPICS)
                {
                    // Cache the topic list and show the list of topics
                    TopicCache = (response as ListTopicsResponse).Topics;
                    Console.WriteLine("[Topic List]" + Environment.NewLine);
                    int index = 0;
                    foreach (NameIdPair topic in TopicCache)
                    {
                        Console.WriteLine("{0}. {1}" + Environment.NewLine, index, topic.Name);
                    }
                }
                else if (response.Type == ResponseType.INFO)
                {
                    // Show received information response
                    Console.WriteLine("[Info] ", (response as InfoResponse).Text);
                }
                waitingForResponse = false;
            }
        }

        /// <summary>
        /// Attempt to send a request to the server if there is a pending request to send
        /// </summary>
        /// <param name="stream">The network stream used for communication with the server</param>
        protected void TrySend(NetworkStream stream)
        {
            if (!waitingForResponse && pendingRequests.Count > 0)
            {
                // Send the pending request to the server
                if (pendingRequests.TryDequeue(out Request request))
                {
                    SendMessage(request, stream);
                    waitingForResponse = true;
                }
            }
        }

        /// <summary>
        /// Add a new pending request to send to the server
        /// </summary>
        /// <param name="request">The request object to send</param>
        public void AddRequest(Request request)
        {
            pendingRequests.Enqueue(request);
        }
    }
}
