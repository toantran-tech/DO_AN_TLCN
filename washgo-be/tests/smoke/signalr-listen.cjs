// Waits for one real-time notification on the WashGo hub and prints it as JSON.
// Usage: node signalr-listen.cjs <baseUrl> <accessToken> [timeoutMs]
// Run `npm install` in this folder first (installs @microsoft/signalr from package.json).
const signalR = require('@microsoft/signalr');

const [baseUrl, token, timeoutMs] = process.argv.slice(2);
const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${baseUrl}/hubs/notifications`, { accessTokenFactory: () => token })
  .configureLogging(signalR.LogLevel.Error)
  .build();

const timer = setTimeout(() => {
  console.log('TIMEOUT');
  process.exit(2);
}, Number(timeoutMs || 15000));

connection.on('notification', async (notification) => {
  clearTimeout(timer);
  console.log(JSON.stringify(notification));
  await connection.stop();
  process.exit(0);
});

connection.start()
  .then(() => console.log('CONNECTED'))
  .catch((error) => {
    console.log(`ERROR ${error}`);
    process.exit(1);
  });
