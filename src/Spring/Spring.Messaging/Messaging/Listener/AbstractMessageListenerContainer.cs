#region License

/*
 * Copyright 2002-2008 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

using System;
using System.Messaging;
using Common.Logging;
using Spring.Context;
using Spring.Messaging.Core;
using Spring.Messaging.Support.Converters;
using Spring.Util;

namespace Spring.Messaging.Listener
{
    /// <summary>
    /// Defines a minimal programming model for message listener containers.  They are expected to
    /// invoke a <see cref="IMessageListener"/> upon asynchronous receives of a MSMQ message.  Access to
    /// obtain MessageQueue and <see cref="IMessageConverter"/> instances is available through the 
    /// <see cref="IMessageQueueFactory"/> property, the default implementation 
    /// <see cref="DefaultMessageQueueFactory"/> provides per-thread instances of these classes.   
    /// </summary>
    /// <author>Mark Pollack</author>
    public abstract class AbstractMessageListenerContainer : AbstractListenerContainer, IApplicationContextAware
    {
        #region Logging Definition

        private static readonly ILog LOG = LogManager.GetLogger(typeof (AbstractMessageListenerContainer));

        #endregion

        #region Fields

        private string messageQueueObjectName;

        private IMessageQueueFactory messageQueueFactory;
        private IApplicationContext applicationContext;

        /// <summary>
        /// Most operations within the MessageListener container hierarchy use methods on the
        /// MessageQueue instance which are thread safe (BeginPeek, BeginReceive, 
        /// EndPeek, EndReceive, GetAllMessages, Peek, and Receive).  When using another
        /// method on the shared MessageQueue instance, wrap calls with a lock on this object.
        /// </summary>
        protected object messageQueueMonitor = new object();

        private IMessageListener messageListener;

        private TimeSpan recoveryTimeSpan = new TimeSpan(0, 0, 0, 1, 0);

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the message queue factory.
        /// </summary>
        /// <value>The message queue factory.</value>
        public IMessageQueueFactory MessageQueueFactory
        {
            get { return messageQueueFactory; }
            set { messageQueueFactory = value; }
        }

        /// <summary>
        /// Gets or sets the name of the message queue object, as refered to in the 
        /// Spring configuration, that will be used to create a DefaultMessageQueue instance
        /// for consuming messages in the container.
        /// </summary>
        /// <value>The name of the message queue object.</value>
        public string MessageQueueObjectName
        {
            get { return messageQueueObjectName; }
            set
            {
                AssertUtils.ArgumentNotNull(value, "MessageQueueObjectName");
                messageQueueObjectName = value;
            }
        }

        /// <summary>
        /// Gets or sets the message listener.
        /// </summary>
        /// <value>The message listener.</value>
        public IMessageListener MessageListener
        {
            get { return messageListener; }
            set
            {
                AssertUtils.ArgumentNotNull(value, "MessageListener");
                messageListener = value;
            }
        }

        /// <summary>
        /// Gets or sets the recovery time span, how long to sleep after an exception in processing occured
        /// to avoid excessive redelivery attempts.  Default value is 1 second.
        /// </summary>
        /// <value>The recovery time span.</value>
        public TimeSpan RecoveryTimeSpan
        {
            get { return recoveryTimeSpan; }
            set { recoveryTimeSpan = value; }
        }

        #endregion

        #region IApplicationContextAware Members

        public IApplicationContext ApplicationContext
        {
            get { return applicationContext; }
            set { applicationContext = value; }
        }

        #endregion

        #region Protected Methods

        protected override void ValidateConfiguration()
        {
            if (MessageQueueObjectName == null)
            {
                throw new ArgumentException("Property 'DefaultMessageQueueObjectName' is required");
            }
            if (MessageQueueFactory == null)
            {
                DefaultMessageQueueFactory qf = new DefaultMessageQueueFactory();
                qf.ApplicationContext = applicationContext;
                MessageQueueFactory = qf;
            }
        }


        protected virtual void DoExecuteListener(Message message)
        {
            if (!Running)
            {
                if (LOG.IsWarnEnabled)
                {
                    LOG.Warn("Not processing recieved message because of the listener container " +
                             "having been stopped in the meantime: " + message);
                }
            }

            InvokeListener(message);
        }

        protected virtual void InvokeListener(Message message)
        {
            if (MessageListener != null)
            {
                DoInvokeListener(MessageListener, message);
            }
            else
            {
                throw new InvalidOperationException("No message listener specified - see property 'MessageListener'");
            }
        }

        protected virtual void DoInvokeListener(IMessageListener listener, Message message)
        {
            listener.OnMessage(message);
        }

        /// <summary>
        /// Closes the queue handle.  Cancel pending receive operation by closing the queue handle
        /// To dispose of the queue handle, set EnableConnectionCache=false and call Close/Dispose.
        /// </summary>
        protected void CloseQueueHandle(MessageQueue mq)
        {
            lock (messageQueueMonitor)
            {
                MessageQueue.EnableConnectionCache = false;
                mq.Close();
                mq.Dispose();
            }
        }

        #endregion
    }
}