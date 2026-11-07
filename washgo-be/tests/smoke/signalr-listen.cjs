// Waits for real-time events on the WashGo hub and prints each one as JSON.
// Usage: node signalr-listen.cjs <baseUrl> <accessToken> [timeoutMs] [eventName=notification] [count=1]
// Run `npm install` in this folder first (installs @microsoft/signalr from package.json).
const signalR = require('@microsoft/signalr');

const [baseUrl, token, timeoutMs, eventName = 'notification', countArg = '1'] = process.argv.slice(2);
const wanted = Number(countArg);
let received = 0;
const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${baseUrl}/hubs/notifications`, { accessTokenFactory: () => token })
  .configureLogging(signalR.LogLevel.Error)
  .build();

const timer = setTimeout(() => {
  console.log('TIMEOUT');
  process.exit(2);
}, Number(timeoutMs || 15000));

connection.on(eventName, async (payload) => {
  console.log(JSON.stringify(payload));
  if (++received < wanted) return;
  clearTimeout(timer);
  await connection.stop();
  process.exit(0);
});

connection.start()
  .then(() => console.log('CONNECTED'))
  .catch((error) => {
    console.log(`ERROR ${error}`);
    process.exit(1);
  });
