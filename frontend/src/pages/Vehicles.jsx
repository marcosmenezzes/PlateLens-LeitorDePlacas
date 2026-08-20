import { useEffect, useMemo, useState } from 'react'
import { apiRequest } from '../api'
import Icon from '../components/Icon'

const empty = { id: null, plate: '', name: '', vehicleType: 'Passeio' }
const types = ['Passeio', 'Caminhonete', 'Caminhão', 'Carreta', 'Desconhecido']
const normalizePlate = (plate) => plate.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 7)
const apiType = (type) => type === 'Caminhão' ? 'Caminhao' : type
const normalizeVehicle = (vehicle) => ({ ...vehicle, vehicleType: vehicle.vehicleType === 'Caminhao' ? 'Caminhão' : vehicle.vehicleType })

/** Lista e mantém os veículos; exclusão também remove o histórico no backend. */
export default function Vehicles() {
  const [vehicles, setVehicles] = useState([])
  const [form, setForm] = useState(empty)
  const [query, setQuery] = useState('')
  const [message, setMessage] = useState('')
  const filtered = useMemo(() => vehicles.filter((vehicle) => `${vehicle.plate} ${vehicle.name}`.toLowerCase().includes(query.toLowerCase())), [vehicles, query])
  useEffect(() => { apiRequest('/api/vehicles').then((items) => setVehicles(items.map(normalizeVehicle))).catch((error) => setMessage(error.message)) }, [])

  /** Valida a placa e cria ou atualiza somente os campos editáveis. */
  async function submit(event) {
    event.preventDefault()
    const plate = normalizePlate(form.plate)
    if (!/^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$/.test(plate)) return setMessage('Informe uma placa brasileira válida.')
    if (vehicles.some((vehicle) => vehicle.plate === plate && vehicle.id !== form.id)) return setMessage('Esta placa já está cadastrada.')
    try {
      const body = JSON.stringify({ plate, name: form.name, vehicleType: apiType(form.vehicleType) })
      const saved = form.id
        ? (await apiRequest(`/api/vehicles/${form.id}`, { method: 'PUT', body }), { ...form, plate })
        : normalizeVehicle(await apiRequest('/api/vehicles', { method: 'POST', body }))
      setVehicles(form.id ? vehicles.map((vehicle) => vehicle.id === form.id ? saved : vehicle) : [...vehicles.filter((vehicle) => vehicle.plate !== plate), saved])
      setForm(empty); setMessage('Veículo salvo no sistema.')
    } catch (error) { setMessage(error.message) }
  }

  /** Confirma e solicita a exclusão atômica do veículo e de seus registros. */
  async function remove(vehicle) {
    if (!window.confirm(`Apagar ${vehicle.plate} e todos os registros dele?`)) return
    try {
      await apiRequest(`/api/vehicles/${vehicle.id}`, { method: 'DELETE' })
      setVehicles(vehicles.filter((item) => item.id !== vehicle.id))
      if (form.id === vehicle.id) setForm(empty)
      setMessage(`Veículo ${vehicle.plate} e seus registros foram apagados.`)
    } catch (error) { setMessage(error.message) }
  }

  return <div className="vehicles-page"><section className="section-intro"><div><span className="eyebrow">CADASTRO</span><h2>Veículos reconhecidos.</h2></div><p>Atualize os desconhecidos uma vez; todo o histórico passa a usar o nome e tipo atuais.</p></section><section className="vehicle-layout"><form className="panel vehicle-form" onSubmit={submit}><div className="panel-heading"><div><span className="eyebrow">{form.id ? 'EDITAR VEÍCULO' : 'NOVO VEÍCULO'}</span><h2>Dados do veículo</h2></div></div><div className="form-fields"><label>Nome<input required maxLength="80" value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} placeholder="Ex.: Carro Marcos" /></label><label>Placa<input required value={form.plate} onChange={(event) => setForm({ ...form, plate: normalizePlate(event.target.value) })} placeholder="ABC1D23" /></label><label>Tipo<select value={form.vehicleType} onChange={(event) => setForm({ ...form, vehicleType: event.target.value })}>{types.map((type) => <option key={type}>{type}</option>)}</select></label><button className="button" type="submit">{form.id ? 'Salvar alterações' : 'Cadastrar veículo'}</button>{form.id && <button className="button button--secondary" type="button" onClick={() => setForm(empty)}>Cancelar</button>}{message && <p className="control-message" role="status">{message}</p>}</div></form><section className="panel vehicle-list"><div className="panel-heading"><div><span className="eyebrow">FROTA E VISITANTES</span><h2>Veículos</h2></div><span className="panel-count">{filtered.length} registros</span></div><div className="vehicle-toolbar"><label>Pesquisar<input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Placa ou nome" /></label></div><div className="table-scroll"><table className="data-table"><thead><tr><th>Placa</th><th>Nome</th><th>Tipo</th><th>Status</th><th>Ação</th></tr></thead><tbody>{filtered.map((vehicle) => <tr key={vehicle.id}><td><strong className="plate-code">{vehicle.plate}</strong></td><td>{vehicle.name}</td><td>{vehicle.vehicleType}</td><td><span className={vehicle.name === 'Desconhecido' ? 'unknown-pill' : 'known-pill'}>{vehicle.name === 'Desconhecido' ? 'Revisar' : 'Conhecido'}</span></td><td><div className="table-actions"><button className="text-link table-action" type="button" onClick={() => { setForm(vehicle); setMessage('') }}>Editar</button><button className="text-link table-action vehicle-delete" type="button" aria-label={`Apagar ${vehicle.plate}`} onClick={() => remove(vehicle)}><Icon name="trash" size={15} /> Apagar</button></div></td></tr>)}</tbody></table></div></section></section></div>
}
