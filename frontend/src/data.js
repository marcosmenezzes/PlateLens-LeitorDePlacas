export const initialVehicles = []

export const accessEvents = []

export const dailyAccess = [
  { date: '14/08', count: 0 }, { date: '15/08', count: 0 }, { date: '16/08', count: 0 },
  { date: '17/08', count: 0 }, { date: '18/08', count: 0 }, { date: '19/08', count: 0 }, { date: '20/08', count: 0 },
]

export const hourlyAccess = [
  { hour: '06h', count: 0 }, { hour: '08h', count: 0 }, { hour: '10h', count: 0 },
  { hour: '12h', count: 0 }, { hour: '14h', count: 0 }, { hour: '16h', count: 0 }, { hour: '18h', count: 0 },
]

const vehiclesKey = 'platelens-vehicles-v2'
localStorage.removeItem('platelens-vehicles')

export function loadVehicles() {
  try { return JSON.parse(localStorage.getItem(vehiclesKey)) || initialVehicles }
  catch { return initialVehicles }
}

export function saveVehicles(vehicles) {
  localStorage.setItem(vehiclesKey, JSON.stringify(vehicles))
}

export function eventsWithVehicles(vehicles = loadVehicles()) {
  return accessEvents.map((event) => ({ ...event, vehicle: vehicles.find((vehicle) => vehicle.id === event.vehicleId) }))
}
