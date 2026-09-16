FROM node:20-alpine

WORKDIR /app

COPY package*.json ./
COPY tsconfig.json ./

RUN npm ci

COPY src ./src
COPY migrations ./migrations

RUN npm run build

CMD ["npm", "start"]
