export default defineEventHandler(async event => (await requestOwnedApi(event, '/api/doctor-information'))._data)
