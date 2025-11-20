const express = require('express');
const bodyParser = require('body-parser');

const app = express();
const port = 3000;

app.use(bodyParser.json());

app.post('/todo', (req, res) => {
    console.log('Received request:', req.body);
    res.json({ status: 'success', message: 'Task added', task: req.body.task });
});

app.listen(port, () => {
    console.log(`Example To-Do Server listening at http://localhost:${port}`);
});
